using System;
namespace DressmakerCritic.Diagnostics
{
    // Exact object identity, never names, timestamps, viewer sibling indices or similarity.
    public static class HistoryImageBinding
    {
        public static int? Find(object progress, object viewerTexture)
        {
            if (viewerTexture == null) return null;
            object records = FieldSnapshot.ResolveFields(progress, "completedQuests");
            object size = FieldSnapshot.ResolveFields(records, "_size");
            if (!(size is int) || (int)size < 0 || (int)size > 512) return null;
            var storage = FieldSnapshot.ResolveFields(records, "_items") as Array;
            if (storage == null || storage.Length < (int)size) return null;
            int? match = null;
            for (int i = 0; i < Math.Min(Math.Max((int)size, 0), 512); i++)
            {
                object record = FieldSnapshot.ListItemAt(records, i);
                object dirty = FieldSnapshot.ResolveFields(record, "thumbnailDirty");
                if (!(dirty is bool) || (bool)dirty || FieldSnapshot.ResolveFields(record, "dress") == null) continue;
                if (!ReferenceEquals(FieldSnapshot.ResolveFields(record, "thumbnailTex"), viewerTexture)) continue;
                if (match.HasValue) return null;
                match = i;
            }
            return match;
        }
    }
}
