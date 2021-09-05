using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using RobloxFiles.DataTypes;

namespace Source2Roblox.World.Types
{
    public class Entity
    {
        public readonly HashSet<Event> Events = new HashSet<Event>();
        public readonly Dictionary<string, object> Fields = new Dictionary<string, object>();

        public string Name => GetString("targetname") ?? ClassName;
        public string ClassName => GetString("classname");
        
        public override string ToString()
        {
            string result = ClassName;

            if (!string.IsNullOrEmpty(Name) && Name != ClassName)
                result += $" \"{Name}\"";

            return result;
        }

        private static object ReadField(string key, string value)
        {
            string[] bySpace = value.Split(' ');
            object result = value;

            if (bySpace.Length > 1)
            {
                bool allNumbers = true;
                var numbers = new List<float>();

                foreach (string token in bySpace)
                {
                    if (float.TryParse(token, out float f))
                    {
                        numbers.Add(f);
                        continue;
                    }
                    
                    allNumbers = false;
                    break;
                }

                if (allNumbers)
                {
                    if (numbers.Count == 4)
                    {
                        byte r = (byte)numbers[0];
                        byte g = (byte)numbers[1];
                        byte b = (byte)numbers[2];

                        var color = new Color3uint8(r, g, b);
                        int brightness = (int)numbers[3];

                        result = new Ambient(color, brightness);
                    }
                    else if (numbers.Count == 3)
                    {
                        if (key.ToLowerInvariant().Contains("color"))
                        {
                            byte r = (byte)numbers[0];
                            byte g = (byte)numbers[1];
                            byte b = (byte)numbers[2];

                            result = new Color3uint8(r, g, b);
                        }
                        else
                        {
                            var xyz = numbers.ToArray();
                            result = new Vector3(xyz);
                        }
                    }
                }
            }
            else if (float.TryParse(value, out float f))
            {
                result = f;
            }

            return result;
        }

        public void AddField(string key, string value)
        {
            string[] byComma = value.Split(',');

            if (byComma.Length == 5)
            {
                string input = byComma[1];
                string param = byComma[2];

                var newEvent = new Event()
                {
                    Output = key,
                    Target = byComma[0],

                    Input = input,
                    Param = ReadField(input, param)
                };
                
                double.TryParse(byComma[3], out newEvent.Delay);
                int.TryParse(byComma[4], out newEvent.FireCount);

                Events.Add(newEvent);
                return;
            }

            object result = ReadField(key, value);
            Fields[key.ToLowerInvariant()] = result;
        }

        public IEnumerable<Event> GetEvents(string name)
        {
            string invariant = name.ToLowerInvariant();
            return Events.Where(str => str.Output == invariant);
        }

        public T? TryGet<T>(string key) where T : struct
        {
            string invariant = key.ToLowerInvariant();

            if (Fields.TryGetValue(invariant, out object obj) && obj is T value)
                return value;

            return null;
        }

        public T Get<T>(string key) where T : class
        {
            string invariant = key.ToLowerInvariant();

            if (Fields.TryGetValue(invariant, out object obj) && obj is T value)
                return value;

            return null;
        }

        public string GetString(string key)
        {
            var value = Get<object>(key);
            return value?.ToString();
        }

        public int? GetInt(string key)
        {
            float? f = TryGet<float>(key);

            if (f != null)
                return (int)f.Value;

            return null;
        }
        
        public static CFrame GetCFrame(Vector3 origin, Vector3 angles)
        {
            const float deg2Rad = (float)Math.PI / 180f;
            const float halfPi = deg2Rad * 90f;

            origin /= Program.STUDS_TO_VMF;
            angles *= deg2Rad;

            var cf = new CFrame(origin.X, origin.Z, -origin.Y)
                   *     CFrame.Angles(0, -halfPi, 0)
                   *     CFrame.Angles(0, angles.Y, 0)
                   *     CFrame.Angles(-angles.X, 0, -angles.Z)
                   *     CFrame.Angles(0, halfPi, 0);

            return cf;
        }

        public CFrame CFrame
        {
            get
            {
                var origin = Get<Vector3>("origin");
                var angles = Get<Vector3>("angles");

                if (origin == null || angles == null)
                    return null;

                return GetCFrame(origin, angles);
            }
        }

        public static void ReadEntities(BinaryReader reader, List<Entity> entities)
        {
            var buffer = new List<byte>();
            reader.ReadToEnd(buffer, reader.ReadByte);

            var bytes = buffer.ToArray();
            string content = Encoding.UTF8.GetString(bytes);

            using (var entReader = new StringReader(content))
            {
                Entity entity = null;
                string line;

                while ((line = entReader.ReadLine()) != null)
                {
                    if (line == null || line == "\0")
                        break;

                    if (line == "{")
                    {
                        entity = new Entity();
                        continue;
                    }
                    else if (line == "}")
                    {
                        entities.Add(entity);
                        continue;
                    }

                    var keyStart = line.IndexOf('"');
                    var keyEnd = line.IndexOf('"', keyStart + 1);

                    var valueStart = line.IndexOf('"', keyEnd + 1);
                    var valueEnd = line.IndexOf('"', valueStart + 1);

                    string key = line.Substring(keyStart + 1, keyEnd - keyStart - 1);
                    string value = line.Substring(valueStart + 1, valueEnd - valueStart - 1);

                    entity.AddField(key, value);
                }
            }
        }
    }
}
