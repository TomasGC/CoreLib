using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Serilog;

namespace CoreLib.Utils {
	public static class Tools {
        static readonly ILogger log = Log.ForContext(MethodBase.GetCurrentMethod().DeclaringType);

        /// <summary>
        /// Remove (and replace) any accents or specials chars.
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        public static string RemoveDiacritics(string str) {
            if (str == null)
                return null;
            var chars =
                from c in str.Normalize(NormalizationForm.FormD).ToCharArray()
                let uc = CharUnicodeInfo.GetUnicodeCategory(c)
                where uc != UnicodeCategory.NonSpacingMark
                select c;

            return new string(chars.ToArray()).Normalize(NormalizationForm.FormC);
        }

        /// <summary>
        /// Return regex matching all accented versions of input characters.
        /// </summary>
        /// <param name="input"></param>
        /// <returns></returns>
        public static string GetDiacriticInsensitiveRegex(string input) {
            return string.Join("", RemoveDiacritics(input ?? string.Empty).Select(c => c switch {
                'a' => "[aàáâãäå]",
                'A' => "[AÀÁÂÃÄÅ]",
                'æ' => "(æ|ae)",
                'Æ' => "(Æ|AE)",
                'c' => "[cç]",
                'C' => "[CÇ]",
                'e' => "[eèéêë]",
                'E' => "[EÈÉÊË]",
                'i' => "[iìíîï]",
                'I' => "[IÌÍÎÏ]",
                'n' => "[nñ]",
                'N' => "[NÑ]",
                'o' => "[oòóôõöø]",
                'O' => "[OÒÓÔÕÖØ]",
                'œ' => "(œ|oe)",
                'Œ' => "(Œ|OE)",
                'ß' => "(ß|ss)",
                's' => "[sš]",
                'S' => "[SŠ]",
                'u' => "[uùúûü]",
                'U' => "[UÚÛÜÙ]",
                'y' => "[yýÿ]",
                'Y' => "[YÝŸ]",
                'z' => "[zž]",
                'Z' => "[ZŽ]",
                _ => c.ToString()
            }));
        }

        /// <summary>
        /// Easier Enum.Parse.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="s"></param>
        /// <returns></returns>
        public static T EnumParse<T>(string s) where T : struct, IConvertible {
            Type type = typeof(T);

            if (string.IsNullOrEmpty(s)) {
                log.Error($"Value is null of empty, cannot parse it.");
                return default;
            }

            if (!type.IsEnum) {
                log.Error($"Could not match '{s}': '{type}' must be an enumerated type");
                return default;
            }

            try {
                return (T)Enum.Parse(type, s);
            }
            catch (Exception e) {
                log.Error($"Matching enum element '{s}' not found in type '{type}': {e.Message}. Stack = [{e.StackTrace}]");
                return default;
            }
        }

        /// <summary>
        /// Get a list of all the values of the enum, we can specify exceptions value if needed.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="exceptions"></param>
        public static List<T> GetListOfEnumValues<T>(params T[] exceptions) {
            List<T> list = new List<T>();

            foreach (T value in Enum.GetValues(typeof(T))) {
                if (exceptions != null && exceptions.Contains(value))
                    continue;

                list.Add(value);
            }

            return list;
        }

        /// <summary>
        /// Compare 2 objects.
        /// </summary>
        /// <param name="object1"></param>
        /// <param name="object2"></param>
        public static bool Equals<T>(T object1, T object2) {
            if (object1 == null && object2 != null)
                return false;

            if (object1 != null && object2 == null)
                return false;

            if (object1 == null && object2 == null)
                return true;

            return object1.Equals(object2);
        }

        /// <summary>
        /// Compare 2 enumerables.
        /// </summary>
        /// <param name="enumerable1"></param>
        /// <param name="enumerable2"></param>
        public static bool SequencesEqual<T>(IEnumerable<T> enumerable1, IEnumerable<T> enumerable2) {
            if (enumerable1 == null && enumerable2 != null)
                return false;

            if (enumerable1 != null && enumerable2 == null)
                return false;

            if (enumerable1 == null && enumerable2 == null)
                return true;

            if (enumerable1.Count() != enumerable2.Count())
                return false;

            return enumerable1.SequenceEqual(enumerable2) && enumerable2.SequenceEqual(enumerable1);
        }

        /// <summary>
        /// Compare 2 dictionnaries.
        /// </summary>
        /// <param name="dictionnary1"></param>
        /// <param name="dictionnary2"></param>
        public static bool SequenceEqual<T, U>(Dictionary<T, U> dictionnary1, Dictionary<T, U> dictionnary2) {
            if (dictionnary1 == null && dictionnary2 != null)
                return false;

            if (dictionnary1 != null && dictionnary2 == null)
                return false;

            if (dictionnary1 == null && dictionnary2 == null)
                return true;

            return dictionnary1.SequenceEqual(dictionnary2) && dictionnary2.SequenceEqual(dictionnary1);
        }

        /// <summary>
        /// Get hash code for IEnumerable fields.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <returns></returns>
        public static int GetHashCodeList<T>(IEnumerable<T> enumerable) => enumerable == null ? 0 : enumerable.Distinct().Aggregate(0, (x, y) => x.GetHashCode() ^ y.GetHashCode());

        /// <summary>
        /// Get hash code for nullable fields.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <returns></returns>
        public static int GetHashCode<T>(T obj) => obj == null ? 0 : obj.GetHashCode();

        /// <summary>
        /// Return the current exact executable root path (folder where is located exe)
        /// </summary>
        /// <returns></returns>
        public static string GetExecutableRootPath() => RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? Environment.CurrentDirectory : Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase).Substring(6); // Because path is like file:/C:\myfolder
	};
}
