﻿using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Joonasw.ElectronicSigningDemo.Documents;

public class BlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public BlobStorageService(BlobServiceClient blobServiceClient, string containerName)
    {
        _blobServiceClient = blobServiceClient;
        _containerName = containerName;
    }

    public async Task<Stream> DownloadAsync(Guid requestId, DocumentType documentType)
    {
        BlobClient blob = GetBlobClient(requestId, documentType);
        Response<BlobDownloadResult> response = await blob.DownloadContentAsync();

        return response.Value.Content.ToStream();
    }

    public async Task UploadAsync(Guid requestId, DocumentType documentType, Stream stream)
    {
        BlobClient blob = GetBlobClient(requestId, documentType);
        await blob.UploadAsync(stream);
    }

    public Task<Stream> OpenWriteAsync(Guid requestId, DocumentType documentType)
    {
        BlobClient blob = GetBlobClient(requestId, documentType);
        return blob.OpenWriteAsync(overwrite: true);
    }

    private BlobClient GetBlobClient(Guid requestId, DocumentType documentType)
    {
        BlobContainerClient container = _blobServiceClient.GetBlobContainerClient(_containerName);
        return container.GetBlobClient(GetBlobName(requestId, documentType));
    }

    private string GetBlobName(Guid requestId, DocumentType documentType) => $"{requestId}/{documentType}";
}
