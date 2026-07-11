namespace BackendApi.Services;

// #136: CommunityController.DownloadMaterial used to redirect the caller's browser straight
// to material.FileUrl with no host restriction beyond "is it http(s)" — a Teacher/Admin who
// set FileUrl to an external phishing domain turned this endpoint into an open redirect
// carrying the platform's trust. Restricting to a configured allowlist of the platform's own
// storage/CDN hosts, enforced both at upload time (fail fast, MaterialsUploadMaterial) and at
// download time (defense in depth, in case a row predates the allowlist or reaches FileUrl by
// some other path). Static/parameterized rather than reading configuration itself so the
// matching rule can be unit tested without booting DI.
public static class MaterialUrlPolicy
{
    public static bool IsAllowedHost(string url, IReadOnlyCollection<string> allowedHosts)
    {
        if (allowedHosts.Count == 0)
        {
            // An empty allowlist means it hasn't been configured — fail closed rather than
            // silently permitting every host, which would defeat the point of the allowlist.
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return allowedHosts.Any(h => string.Equals(h, uri.Host, StringComparison.OrdinalIgnoreCase));
    }
}
