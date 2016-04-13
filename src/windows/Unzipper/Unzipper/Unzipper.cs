using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Unzipper
{
    /// <summary>
    /// Provides status information about an unzip in progress
    /// </summary>
    public sealed class Status
    {
        public long loaded { get; set; }
        public long total { get; set; }
    }

    public sealed class Unzipper
    {
        /// <summary>
        /// Unzips a given zip file to the destination folder.
        /// Provides progress in uncompressed bytes
        /// </summary>
        /// <param name="zipfilepath">File path or HTML5 File API URI to a valid zip file</param>
        /// <param name="destinationfolder">File path or HTML5 File API URI to the folder to hold the unzipped archive</param>
        /// <returns></returns>
        public static IAsyncActionWithProgress<Status> Unzip(string zipfilepath, string destinationfolder)
        {
            return AsyncInfo.Run<Status>((token, progress) =>
            Task.Run
            ( async() =>
            {
                await UnZipFileHelper(zipfilepath, destinationfolder,progress).AsAsyncAction();
                return new Status() { loaded = 100, total = 100 };
            }
            ,token)
            );


        }

        private static async Task UnZipFileHelper(string zipfilepath, string destinationfolderpath, IProgress<Status> progress)
        {
                //System.Diagnostics.Debug.WriteLine("current folder: " + ApplicationData.Current.LocalFolder.Path);
                //System.Diagnostics.Debug.WriteLine("zipfile: " + zipfilepath);
                //System.Diagnostics.Debug.WriteLine("destination: " + destinationfolderpath);


                if (zipfilepath.StartsWith(@"\"))
                {
                    zipfilepath = ApplicationData.Current.LocalFolder.Path + zipfilepath;
                }

                if (destinationfolderpath.StartsWith(@"\"))
                {
                    destinationfolderpath = ApplicationData.Current.LocalFolder.Path + destinationfolderpath;
                }


                StorageFolder destinationFolder;

                if (IsFileAPIURI(destinationfolderpath))
                {
                    //System.Diagnostics.Debug.WriteLine("destinationfolderpath is a fileapi uri.");


                    destinationFolder = await GetFolderFromURI(destinationfolderpath);
                }
                else
                {
                    destinationFolder = await StorageFolder.GetFolderFromPathAsync(destinationfolderpath);
                }

                System.Diagnostics.Debug.WriteLine("2: " + destinationFolder.Path);        
                StorageFile zipFile;

                if (IsFileAPIURI(zipfilepath))
                {
                    //System.Diagnostics.Debug.WriteLine("zipfilepath is a fileapi uri.");
                    zipFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(zipfilepath));
                }
                else
                {
                    zipFile = await StorageFile.GetFileFromPathAsync(zipfilepath);
                }

                System.Diagnostics.Debug.WriteLine("3: " + zipFile.Path);
                Stream zipStream = await zipFile.OpenStreamForReadAsync();
                System.Diagnostics.Debug.WriteLine("4");



                using (ZipArchive zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                {
                    //report compressed size
                    long totalLength = zipArchive.Entries.Select(x => x.Length).Sum();

                    //System.Diagnostics.Debug.WriteLine("Reporting progress");
                    progress.Report(new Status() { loaded = 0, total = totalLength });

                    long loaded = 0;

                    foreach (ZipArchiveEntry entry in zipArchive.Entries)
                    {
                        await UnzipZipArchiveEntryAsync(entry, entry.FullName, destinationFolder);
                        loaded += entry.Length;
                        //System.Diagnostics.Debug.WriteLine("Reporting progress");
                        progress.Report(new Status() { loaded = loaded, total = totalLength });
                    }
                }
        }

        private static async Task UnzipZipArchiveEntryAsync(ZipArchiveEntry entry, string filePath, StorageFolder unzipFolder)
        {
            if (IfPathContainDirectory(filePath))
            {
                // Create sub folder 
                string subFolderName = Path.GetDirectoryName(filePath);
                bool isSubFolderExist = await IfFolderExistsAsync(unzipFolder, subFolderName);
                StorageFolder subFolder;
                if (!isSubFolderExist)
                {
                    // Create the sub folder. 
                    subFolder =
                        await unzipFolder.CreateFolderAsync(subFolderName, CreationCollisionOption.ReplaceExisting);
                }
                else
                {
                    // Just get the folder. 
                    subFolder =
                        await unzipFolder.GetFolderAsync(subFolderName);
                }
                // All sub folders have been created. Just pass the file name to the Unzip function. 
                string newFilePath = Path.GetFileName(filePath);
                if (!string.IsNullOrEmpty(newFilePath))
                {
                    // Unzip file iteratively. 
                    await UnzipZipArchiveEntryAsync(entry, newFilePath, subFolder);
                }
            }
            else
            {
                // Read uncompressed contents 
                using (Stream entryStream = entry.Open())
                {
                    byte[] buffer = new byte[entry.Length];
                    entryStream.Read(buffer, 0, buffer.Length);

                    StorageFile uncompressedFile = await unzipFolder.CreateFileAsync
                    (entry.Name, CreationCollisionOption.ReplaceExisting);

                    using (IRandomAccessStream uncompressedFileStream =
                    await uncompressedFile.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        using (Stream outstream = uncompressedFileStream.AsStreamForWrite())
                        {
                            outstream.Write(buffer, 0, buffer.Length);
                            outstream.Flush();
                        }
                    }
                }
            }
        }

        /// <summary> 
        /// It checks if the specified path contains directory. 
        /// </summary> 
        /// <param name="entryPath">The specified path</param> 
        /// <returns></returns> 
        private static bool IfPathContainDirectory(string entryPath)
        {
            if (string.IsNullOrEmpty(entryPath))
            {
                return false;
            }
            return entryPath.Contains("/");
        }
        /// <summary> 
        /// It checks if the specified folder exists. 
        /// </summary> 
        /// <param name="storageFolder">The container folder</param> 
        /// <param name="subFolderName">The sub folder name</param> 
        /// <returns></returns> 
        private static async Task<bool> IfFolderExistsAsync(StorageFolder storageFolder, string subFolderName)
        {
            try
            {
                await storageFolder.GetFolderAsync(subFolderName);
            }
            catch (FileNotFoundException)
            {
                return false;
            }
            catch (Exception)
            {
                throw;
            }
            return true;
        }

        /// <summary>
        /// Tests if the given path is in the
        /// format of a HTML5 File API URL
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool IsFileAPIURI(string path)
        {
            if(path.StartsWith("ms-appdata://"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a storage folder from a given HTML5
        /// File API URI
        /// </summary>
        /// <param name="uri"></param>
        /// <returns></returns>
        private static async Task<StorageFolder> GetFolderFromURI(string uri)
        {
            if(!uri.StartsWith("ms-appdata://"))
            {
                throw new Exception("Incorrect URI type");
            }

            uri = uri.Replace("ms-appdata:///", "");
            System.Diagnostics.Debug.WriteLine("URI 1: " + uri);


           string area = uri.Split('/')[0];

            string path;

            switch(area)
            {
                case "temp":
                    path = ApplicationData.Current.TemporaryFolder.Path;
                    break;
                case "local":
                    path = ApplicationData.Current.LocalFolder.Path;
                    break;
                default:
                    throw new Exception("Unknown Location");
            }
            System.Diagnostics.Debug.WriteLine("PATH 1: " + path);

            //trim off the area
            int index = uri.IndexOf('/');
            uri = uri.Substring(index);

            uri = uri.Replace('/','\\');

            System.Diagnostics.Debug.WriteLine("URI 2: " + uri);

            //add any path after area:
            path = path + uri;
            System.Diagnostics.Debug.WriteLine("PATH 2: " + path);

            return await StorageFolder.GetFolderFromPathAsync(path);
        }

    }
}
