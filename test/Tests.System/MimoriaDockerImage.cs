// SPDX-FileCopyrightText: 2025 varelen
//
// SPDX-License-Identifier: MIT

using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Images;

namespace Varelen.Mimoria.Tests.System;

public static class MimoriaDockerImage
{
    private static IFutureDockerImage? image = null;

    public static async Task<IFutureDockerImage> GetOrCreateImageAsync()
    {
        if (image is not null)
        {
            return image;
        }

        image = new ImageFromDockerfileBuilder()
            .WithDockerfileDirectory(CommonDirectoryPath.GetSolutionDirectory(), string.Empty)
            .WithDockerfile("src/Service/Dockerfile")
            .Build();

        await image.CreateAsync();

        return image;
    }
}
