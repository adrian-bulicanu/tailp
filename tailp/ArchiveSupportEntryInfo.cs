using System;
using SharpCompress.Archives;

namespace tailp
{
    public class ArchiveSupportEntryInfo
    {
        private string Path { get; }
        public long Size { get; }
        public DateTime CreatedTime { get; }

        public ArchiveSupportEntryInfo(IArchiveEntry entry)
        {
            if (entry is null) throw new ArgumentNullException(nameof(entry));

            Path = entry.Key;
            Size = entry.Size;
            CreatedTime = entry.CreatedTime.GetValueOrDefault();
        }

        public override int GetHashCode() => Path.GetHashCode(StringComparison.OrdinalIgnoreCase);

        public override bool Equals(object obj)
        {
            if (obj == null) return false;

            if (obj is ArchiveSupportEntryInfo e)
            {
                return Path.Equals(e.Path, StringComparison.OrdinalIgnoreCase);
            }

            if (obj is string s)
            {
                return Path.Equals(s, StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }
    }
}