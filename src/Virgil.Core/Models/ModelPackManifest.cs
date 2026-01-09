using System;

namespace Virgil.Core.Models;

public sealed record ModelPackManifest(Uri DownloadUri, string FileName, string Sha256);
