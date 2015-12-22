using System;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.Zip;
using System.IO;

namespace Comdata.AppSupport.AppTools.Zip
{
    public class Zip
    {
        public static void Compress(string source, string archive)
        {
			// Depending on the directory this could be very large and would require more attention
			// in a commercial package.
            // get the file attributes for file or directory
            string[] filenames;
            var attr = File.GetAttributes(source);

            if((attr & FileAttributes.Directory) == FileAttributes.Directory)
                filenames = Directory.GetFiles(source);
            else 
            {
                filenames=new string[1];
                filenames[0] = source;
            }
			
			// 'using' statements guarantee the stream is closed properly which is a big source
			// of problems otherwise.  Its exception safe as well which is great.
            try
            {
			    using (ZipOutputStream s = new ZipOutputStream(File.Create(archive))) 
                {
				    s.SetLevel(9); // 0 - store only to 9 - means best compression
				    byte[] buffer = new byte[4096];
		
                    foreach (string file in filenames)
                    {
					    // Using GetFileName makes the result compatible with XP
					    // as the resulting path is not absolute.
					    ZipEntry entry = new ZipEntry(Path.GetFileName(file));
					
					    // Setup the entry data as required.
					    // Crc and size are handled by the library for seakable streams
					    // so no need to do them here.

					    // Could also use the last write time or similar for the file.
                        entry.DateTime = File.GetLastWriteTime(file);
					    s.PutNextEntry(entry);
					
					    using ( FileStream fs = File.OpenRead(file))
                        {
						    // Using a fixed size buffer here makes no noticeable difference for output
						    // but keeps a lid on memory usage.
						    int sourceBytes;
						
                            do 
                            {
							    sourceBytes = fs.Read(buffer, 0, buffer.Length);
							    s.Write(buffer, 0, sourceBytes);
						    } 
                            while ( sourceBytes > 0 );
					    }
				    }
				
				    s.Finish();
				    s.Close();
			    }
		    }
		    catch(Exception ex)
		    {
                throw new Exception ("Error creating zip archive " + archive, ex);
            }
        }

        public static void Extract(string archiveFilename, string password, string outFolder)
        {
            ZipFile zf = null;

            try
            {
                FileStream fs = File.OpenRead(archiveFilename);
                zf = new ZipFile(fs);

                if (!String.IsNullOrEmpty(password))
                    zf.Password = password;     // AES encrypted entries are handled automatically

                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                        continue;           // Ignore directories

                    String entryFileName = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);

                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                }
            }

            catch (Exception)
            {
                throw new Exception(string.Format("An Error has occured while extracting: {0} ", archiveFilename));
            }

            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
        }

        public static void Search(string archiveFilename, string password, string outFolder, string filename, out string zipEntryFilename, out string extractedFilename)
        {
            zipEntryFilename = string.Empty;
            extractedFilename = string.Empty;
            ZipFile zf = null;

            try
            {
                FileStream fs = File.OpenRead(archiveFilename);
                zf = new ZipFile(fs);

                if (!String.IsNullOrEmpty(password))
                    zf.Password = password;     // AES encrypted entries are handled automatically

                var nodes = filename.Split(new char[] { '_' });
                var entryNumber = -1;

                if (nodes.Length > 2)
                    if (!int.TryParse(nodes[nodes.Length-2], out entryNumber))
                    {
                        nodes = nodes[2].Split(new char[] { '.' });
                        entryNumber = int.Parse(nodes[0]);
                    }

                var entry = 0;

                foreach (ZipEntry zipEntry in zf)
                {
                    if (!zipEntry.IsFile)
                        continue;           // Ignore directories

                    if (++entry != entryNumber && entryNumber != -1)
                        continue;

                   // if (!filename.Contains(zipEntry.Name.ToUpper()))
                   //     continue;

                    String entryFileName = filename;
                    zipEntryFilename = zipEntry.Name;
                    // to remove the folder from the entry:- entryFileName = Path.GetFileName(entryFileName);
                    // Optionally match entrynames against a selection list here to skip as desired.
                    // The unpacked length is available in the zipEntry.Size property.

                    byte[] buffer = new byte[4096];     // 4K is optimum
                    Stream zipStream = zf.GetInputStream(zipEntry);

                    // Manipulate the output filename here as desired.
                    String fullZipToPath = Path.Combine(outFolder, entryFileName);
                    string directoryName = Path.GetDirectoryName(fullZipToPath);

                    if (directoryName.Length > 0)
                        Directory.CreateDirectory(directoryName);

                    // Unzip file in buffered chunks. This is just as fast as unpacking to a buffer the full size
                    // of the file, but does not waste memory.
                    // The "using" will close the stream even if an exception occurs.
                    using (FileStream streamWriter = File.Create(fullZipToPath))
                        StreamUtils.Copy(zipStream, streamWriter, buffer);
                    extractedFilename = fullZipToPath;
                    break;
                }
            }

            catch (Exception)
            {
                throw new Exception (string.Format("An Error has occured while locating {0} in {1}", zipEntryFilename, archiveFilename));
            }

            finally
            {
                if (zf != null)
                {
                    zf.IsStreamOwner = true; // Makes close also shut the underlying stream
                    zf.Close(); // Ensure we release resources
                }
            }
        }
    }
}
