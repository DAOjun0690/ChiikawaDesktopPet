// src/ChiikawaDesktopPet.Wpf.Tests/CharacterAssetPackageTests.cs
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Threading;
using ChiikawaDesktopPet.Wpf;
using Xunit;

namespace ChiikawaDesktopPet.Wpf.Tests;

public class CharacterAssetPackageTests
{
    private static void RunInSta(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                exception = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception != null)
        {
            throw new Exception("STA thread failed", exception);
        }
    }

    private static void CreateDummyPng(string path, int width = 32, int height = 32)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(200, 255, 100, 100));
        }
        bmp.Save(path, ImageFormat.Png);
    }

    [Fact]
    public void DirectoryCharacterAssetPackage_DiscoversAndLoadsSpritesAndFrames()
    {
        RunInSta(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"dir-asset-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);

            try
            {
                CreateDummyPng(Path.Combine(tempDir, "sprites", "spawn.png"));
                CreateDummyPng(Path.Combine(tempDir, "sprites", "shaken.png"));
                CreateDummyPng(Path.Combine(tempDir, "sprites", "jumpleft.png"));
                CreateDummyPng(Path.Combine(tempDir, "sprites", "jumpright.png"));
                CreateDummyPng(Path.Combine(tempDir, "animations", "bounce", "0-bounce.png"));
                CreateDummyPng(Path.Combine(tempDir, "animations", "dance", "0-dance.png"));
                CreateDummyPng(Path.Combine(tempDir, "animations", "dance", "1-dance.png"));

                using var package = new DirectoryCharacterAssetPackage("testchar", tempDir);

                Assert.True(package.HasSprite("spawn"));
                Assert.True(package.HasSprite("shaken"));
                Assert.False(package.HasSprite("nonexistent"));

                Assert.True(package.HasAnimation("bounce"));
                Assert.True(package.HasAnimation("dance"));
                Assert.False(package.HasAnimation("nonexistent"));

                Assert.True(package.HasDirectionalCapability("jumpleft", "jumpright"));

                var otherAnims = package.DiscoverAnimationNames(new HashSet<string>(["bounce"]));
                Assert.Contains("dance", otherAnims);
                Assert.DoesNotContain("bounce", otherAnims);

                var sprites = package.LoadStaticSprites(100, 100);
                Assert.True(sprites.ContainsKey("spawn"));
                Assert.True(sprites.ContainsKey("shaken"));

                var frames = package.LoadAnimationFrames("dance", 100, 100);
                Assert.Equal(2, frames.Count);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        });
    }

    [Fact]
    public void ZipCharacterAssetPackage_DiscoversAndLoadsSpritesAndFrames()
    {
        RunInSta(() =>
        {
            string tempDir = Path.Combine(Path.GetTempPath(), $"zip-asset-test-{Guid.NewGuid()}");
            Directory.CreateDirectory(tempDir);
            string tempZip = Path.Combine(tempDir, "testchar.zip");

            try
            {
                using (var fs = File.Create(tempZip))
                using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    void AddDummyPng(string entryName)
                    {
                        var entry = archive.CreateEntry(entryName);
                        using var s = entry.Open();
                        using var bmp = new Bitmap(32, 32, PixelFormat.Format32bppArgb);
                        using (var g = Graphics.FromImage(bmp)) g.Clear(Color.Gold);
                        bmp.Save(s, ImageFormat.Png);
                    }

                    AddDummyPng("sprites/spawn.png");
                    AddDummyPng("sprites/shaken.png");
                    AddDummyPng("sprites/jumpleft.png");
                    AddDummyPng("sprites/jumpright.png");
                    AddDummyPng("animations/bounce/0-bounce.png");
                    AddDummyPng("animations/dance/0-dance.png");
                    AddDummyPng("animations/dance/1-dance.png");
                }

                using var package = new ZipCharacterAssetPackage("testchar", tempZip);

                Assert.True(package.HasSprite("spawn"));
                Assert.True(package.HasSprite("shaken"));
                Assert.False(package.HasSprite("nonexistent"));

                Assert.True(package.HasAnimation("bounce"));
                Assert.True(package.HasAnimation("dance"));
                Assert.False(package.HasAnimation("nonexistent"));

                Assert.True(package.HasDirectionalCapability("jumpleft", "jumpright"));

                var otherAnims = package.DiscoverAnimationNames(new HashSet<string>(["bounce"]));
                Assert.Contains("dance", otherAnims);
                Assert.DoesNotContain("bounce", otherAnims);

                var sprites = package.LoadStaticSprites(100, 100);
                Assert.True(sprites.ContainsKey("spawn"));
                Assert.True(sprites.ContainsKey("shaken"));

                var frames = package.LoadAnimationFrames("dance", 100, 100);
                Assert.Equal(2, frames.Count);
            }
            finally
            {
                Directory.Delete(tempDir, true);
            }
        });
    }

    [Fact]
    public void CharacterAssetPackage_Open_SelectsZipWhenDirectoryMissing()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), $"open-asset-test-{Guid.NewGuid()}");
        string assetsDir = Path.Combine(tempDir, "assets");
        Directory.CreateDirectory(assetsDir);
        string zipPath = Path.Combine(assetsDir, "testchar.zip");

        try
        {
            using (var fs = File.Create(zipPath))
            using (var archive = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                archive.CreateEntry("sprites/spawn.png");
            }

            using var package = CharacterAssetPackage.Open("testchar", tempDir);
            Assert.IsType<ZipCharacterAssetPackage>(package);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
