namespace JouleDesk;

/// <summary>Format of the drive profile catalog, both the bundled file and the optional online update.</summary>
internal sealed record CatalogDocument(int Version, List<DiskProfile> Disks);
