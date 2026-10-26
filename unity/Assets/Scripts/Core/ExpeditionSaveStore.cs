using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace Expedition.Core
{
    public enum SaveReadState { Empty, Loaded, Recovered, Damaged, FutureVersion }
    public sealed class FutureSaveVersionException : Exception { }
    public sealed class SaveReadResult
    {
        public SaveReadState State;
        public string Payload, Source;
        public long Generation;
    }

    // Alternate whole-account files. A torn write never touches the last valid generation.
    // The checksum detects damage; it is not an anti-cheat signature.
    public sealed class ExpeditionSaveStore
    {
        const string Magic = "IGNIS-SAVE-1";
        const int MaxBytes = 4 * 1024 * 1024;
        readonly string first, second;
        readonly Action<string> validate;
        static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        public ExpeditionSaveStore(string directory, Action<string> validator)
        {
            first = Path.Combine(directory, "account-a.sav");
            second = Path.Combine(directory, "account-b.sav");
            if (validator == null) throw new ArgumentNullException("validator");
            validate = validator;
        }
        static string Hash(string value)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Utf8.GetBytes(value))).Replace("-", "");
        }
        SaveReadResult Read(string path)
        {
            if (!File.Exists(path)) return new SaveReadResult { State = SaveReadState.Empty, Source = path };
            try
            {
                if (new FileInfo(path).Length > MaxBytes) throw new InvalidDataException();
                string[] parts = File.ReadAllText(path, Utf8).Split(new[] { '\n' }, 4);
                if (parts.Length != 4) throw new InvalidDataException();
                if (parts[0] != Magic)
                {
                    if (parts[0].StartsWith("IGNIS-SAVE-", StringComparison.Ordinal)) throw new FutureSaveVersionException();
                    throw new InvalidDataException();
                }
                long generation;
                if (!long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out generation) || generation < 1 ||
                    parts[2] != Hash(parts[1] + "\n" + parts[3])) throw new InvalidDataException();
                validate(parts[3]);
                return new SaveReadResult { State = SaveReadState.Loaded, Source = path, Generation = generation, Payload = parts[3] };
            }
            catch (FutureSaveVersionException) { return new SaveReadResult { State = SaveReadState.FutureVersion, Source = path }; }
            catch (Exception) { return new SaveReadResult { State = SaveReadState.Damaged, Source = path }; }
        }
        public SaveReadResult Load()
        {
            var a = Read(first); var b = Read(second);
            if (a.State == SaveReadState.FutureVersion || b.State == SaveReadState.FutureVersion)
                return new SaveReadResult { State = SaveReadState.FutureVersion };
            bool av = a.State == SaveReadState.Loaded, bv = b.State == SaveReadState.Loaded;
            if (!av && !bv) return new SaveReadResult { State = a.State == SaveReadState.Empty && b.State == SaveReadState.Empty ? SaveReadState.Empty : SaveReadState.Damaged };
            var result = av && (!bv || a.Generation >= b.Generation) ? a : b;
            if (!av || !bv) result.State = SaveReadState.Recovered;
            return result;
        }
        void Write(string path, long generation, string payload)
        {
            string number = generation.ToString(CultureInfo.InvariantCulture);
            byte[] data = Utf8.GetBytes(Magic + "\n" + number + "\n" + Hash(number + "\n" + payload) + "\n" + payload);
            if (data.Length > MaxBytes) throw new InvalidDataException("Save is too large.");
            if (File.Exists(path) && Read(path).State == SaveReadState.Damaged)
                File.Copy(path, path + ".damaged", true);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            { stream.Write(data, 0, data.Length); stream.Flush(true); }
            var check = Read(path);
            if (check.State != SaveReadState.Loaded || check.Payload != payload) throw new IOException("Save verification failed.");
        }
        public void Commit(string payload)
        {
            validate(payload);
            var previous = Load();
            if (previous.State == SaveReadState.Damaged || previous.State == SaveReadState.FutureVersion)
                throw new InvalidDataException("Existing account requires recovery; refusing to overwrite it.");
            long next = checked(previous.Generation + 1);
            string target = previous.Source == first ? second : first;
            Write(target, next, payload);
            if (previous.State == SaveReadState.Empty) Write(second, next, payload);
        }
    }
}
