using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using RobloxFiles;

using Source2Roblox.Forms;

using System.Threading;
using RobloxFiles.DataTypes;
using System.Net.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Source2Roblox.Upload
{
    public struct AssetUploadResponse
    {
        public bool Success;
        public string Message;

        public long? AssetId;
        public long? BackingAssetId;
    }

    public class AssetManager
    {
        private string XsrfToken;

        private const string uploadAsset = "https://apis.roblox.com/assets/v1/assets";
        private const string getOperation = "https://apis.roblox.com/assets/v1/operations";

        private const string apikey = "";
        private const string userId = "";

        private const string ROBLOX_COOKIE = "";

        private const string UploadDecal = "https://data.roblox.com/Data/Upload.ashx?json=1&type=Decal";
        private const string UploadMesh = "https://data.roblox.com/ide/publish/UploadNewMesh?";

        private const string decalToImage = "https://f3xteam.com/bt/getDecalImageID/";

        public readonly string RootDir;
        public readonly string RbxAssetDir;

        private readonly SemaphoreSlim UploadSemaphore = new SemaphoreSlim(1, 4);
        private readonly Dictionary<string, bool> UploadConsent = new Dictionary<string, bool>();


        public async Task<string> Get(string url)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    HttpResponseMessage response = await client.GetAsync(url);
                    response.EnsureSuccessStatusCode();
                    string responseBody = await response.Content.ReadAsStringAsync();
                    return responseBody;
                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine($"Error: {e.Message}");
                    return null;
                }
            }
        }

        public AssetManager(string rootDir, string rbxAssetDir)
        {
            RootDir = rootDir;
            RbxAssetDir = rbxAssetDir;
        }

        public Func<Task<string>> BatchAssetId(string localPath)
        {
            if (localPath.EndsWith(".vtf"))
                localPath = localPath.Replace(".vtf", ".png");

            string filePath = Path.Combine(RootDir, localPath);
            byte[] content;

            if (File.Exists(filePath))
                content = File.ReadAllBytes(filePath);
            else
                content = Array.Empty<byte>();

            var info = new FileInfo(filePath);
            string dir = info.DirectoryName;

            string fileName = info.Name;
            string assetPath = filePath + ".asset";

            if (fileName.EndsWith(".mesh"))
            {
                if (fileName.StartsWith("cluster_") || fileName.StartsWith("disp_"))
                {
                    var assetHash = SharedString
                        .FromBuffer(content)
                        .ComputedKey
                        .Replace("+", "-")
                        .Replace("/", "_")
                        .TrimEnd('=');

                    string assetsDir = Path.Combine(dir, "assets");
                    assetPath = Path.Combine(assetsDir, assetHash + ".asset");

                    Directory.CreateDirectory(assetsDir);
                }
            }

            if (!File.Exists(assetPath) && !Program.LOCAL_ONLY)
            {
                if (!UploadConsent.TryGetValue(assetPath, out bool canUpload))
                {
                    var uploadForm = new Uploader(filePath);

                    if (!uploadForm.IsDisposed)
                        uploadForm.ShowDialog();

                    canUpload = uploadForm.Upload;
                    UploadConsent[assetPath] = canUpload;
                }

                if (canUpload)
                {
                    return () => Task.Run(async () =>
                    {
                        if (File.Exists(assetPath))
                        {
                            string assetId = File.ReadAllText(assetPath);
                            return $"rbxassetid://{assetId}";
                        }

                        var extension = info.Extension;
                        string name = info.Name.Replace(extension, "");

                        if (extension != "")
                        {
                            string uploadName = name;
                            string uploadDesc = "source to roblox";

                            for (int retry = 0; retry < 5; retry++)
                            {
                                try
                                {
                                    if (extension == ".png")
                                    {

                                        var requestContent = new MultipartFormDataContent();
                                        requestContent.Add(new StringContent("{ \"assetType\": \"Decal\",  \"displayName\": \"" + uploadName + "\", \"description\": \"" + uploadDesc + "\", \"creationContext\": { \"creator\": { \"userId\": \"" + userId + "\" } } }"), "request");
                                        requestContent.Add(new StreamContent(File.OpenRead(filePath)), "fileContent", Path.GetFileName(filePath));

                                        var client = new HttpClient();
                                        client.DefaultRequestHeaders.Add("x-api-key", apikey);
                                        var response = await client.PostAsync(uploadAsset, requestContent);

                                        var responseContent = await response.Content.ReadAsStringAsync();
                                        var responseJson = JObject.Parse(responseContent);

                                        if (responseJson["message"] != null)
                                        {
                                            throw new Exception(responseJson["message"].ToString());
                                        }
                                        string pathValue = responseJson["path"].ToString();

                                        string[] pathParts = pathValue.Split(new string[] { "operations/" }, StringSplitOptions.None);

                                        string operationId = pathParts[1];

                                        string getOperationURL = $"{getOperation}/{operationId}";

                                        var response_2 = await client.GetAsync(getOperationURL);

                                        var response_2Content = await response_2.Content.ReadAsStringAsync();

                                        var response_2Json = JObject.Parse(response_2Content);

                                        bool isDone = (bool)response_2Json["done"];
                                        if (!isDone)
                                        {
                                            throw new Exception("something went wrong and the asset is still uploading even after a while");
                                        }

                                        string assetId = response_2Json["response"]["assetId"].ToString();
                                        if (string.IsNullOrEmpty(assetId))
                                        {
                                            throw new Exception("wtf lmao");
                                        }

                                        var decalToImageResponse = await client.GetAsync(decalToImage+assetId);

                                        string decalToImageId = await decalToImageResponse.Content.ReadAsStringAsync();

                                        
                                        if (string.IsNullOrEmpty(decalToImageId))
                                        {
                                            throw new Exception("NOT COOL man.");
                                        }

                                        File.WriteAllText(assetPath, decalToImageId);
                                        Console.WriteLine($"Uploaded {localPath}: {decalToImageId} -> {assetPath}");
                                        await Task.Delay(500);
                                        break;
                                    }
                                    else if (extension == ".mesh")
                                    {

                                        string postUrl = $"{UploadMesh}name={WebUtility.UrlEncode(uploadName)}&description={WebUtility.UrlEncode(uploadDesc)}";
                                        var request = WebRequest.Create(postUrl) as HttpWebRequest;

                                        request.Method = "POST";
                                        request.ContentType = "*/*";
                                        request.UserAgent = "RobloxStudio/WinInet";

                                        request.Headers.Set("Cookie", $".ROBLOSECURITY={ROBLOX_COOKIE}");
                                        request.Headers.Set("X-CSRF-TOKEN", XsrfToken);

                                        using (var writeStream = request.GetRequestStream())
                                        {
                                            writeStream.Write(content, 0, content.Length);
                                            writeStream.Close();
                                        }

                                        var webResponse = request.GetResponse() as HttpWebResponse;

                                        using (var readStream = webResponse.GetResponseStream())
                                        using (var reader = new StreamReader(readStream))
                                        {
                                            string response = reader.ReadToEnd();
                                            string asset = response.ToString();

                                            if (!long.TryParse(response, out long assetId))
                                            {
                                                var upload = JsonConvert.DeserializeObject<AssetUploadResponse>(response);
                                                if (!upload.Success)
                                                    throw new Exception(upload.Message);

                                                asset = upload.BackingAssetId.ToString();
                                            }

                                            File.WriteAllText(assetPath, asset);
                                            Console.WriteLine($"Uploaded {localPath}: {asset} -> {assetPath}");

                                            await Task.Delay(500);
                                            break;
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    bool xsrf = false;

                                    if (e is WebException webEx)
                                    {
                                        var response = webEx.Response as HttpWebResponse;
                                        xsrf = response.StatusDescription.Contains("XSRF");

                                        if (xsrf)
                                            XsrfToken = response.Headers.Get("X-CSRF-TOKEN");

                                        response.Close();
                                    }

                                    if (xsrf)
                                        continue;

                                    Console.WriteLine(e.Message);
                                    if (!e.Message.ToLower().Contains("moderated") && !e.Message.ToLower().Contains("inappropriate"))
                                    {
                                        Console.WriteLine("Cooling down from possible asset overload...");
                                        await Task.Delay(30000);
                                        continue;
                                    }
                                    uploadName = "source to roblox";
                                }
                            }
                        }

                        if (File.Exists(assetPath))
                        {
                            string assetId = File.ReadAllText(assetPath);
                            return $"rbxassetid://{assetId}";
                        }

                        return $"{RbxAssetDir}/{localPath}";
                    });
                }
            }

            return () => Task.Run(() =>
            {
                if (File.Exists(assetPath) && !Program.LOCAL_ONLY)
                {
                    string assetId = File.ReadAllText(assetPath);
                    return $"rbxassetid://{assetId}";
                }

                return $"{RbxAssetDir}/{localPath}";
            });
        }

        public void BindAssetId(string localPath, List<Task> uploadPool, Instance target, string property)
        {
            if (string.IsNullOrEmpty(apikey) | string.IsNullOrEmpty(userId))
            {
                Console.WriteLine("[AssetUploader] apikey or userId variables are empty, open Util/AssetManager.cs to edit the file with your own");
                return;
            }
            Property prop = target.GetProperty(property);

            if (prop == null)
                throw new Exception($"Unknown property {property} in {target.ClassName}");

            // Prompt should be synchronously shown to user.
            var batchAssetId = BatchAssetId(localPath);

            // Hand off upload to asynchronous task.
            Task bind = Task.Run(async () =>
            {
                await UploadSemaphore.WaitAsync();
                var getAssetId = batchAssetId();

                string assetId = await getAssetId.ConfigureAwait(false);
                prop.Value = assetId;

                UploadSemaphore.Release();
            });

            // Add to the upload task pool.
            uploadPool.Add(bind);
        }
    }
}
