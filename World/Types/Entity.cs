using System;
using System.Collections.Generic;
using System.Linq;

using RobloxFiles.DataTypes;

namespace Source2Roblox.World.Types
{
    public class Entity
    {
        public readonly HashSet<Event> Events = new HashSet<Event>();
        public readonly Dictionary<string, object> Fields = new Dictionary<string, object>();

        public int HammerId => GetInt("hammerId") ?? -1;
        public string ClassName => GetString("className");
        public override string ToString() => $"[{HammerId}] {ClassName}";

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

                if (allNumbers && numbers.Count == 3)
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
            Fields.Add(key.ToLowerInvariant(), result);
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
            return (int?)f;
        }
    }
}
