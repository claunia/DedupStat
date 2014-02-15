/*******************************************************************************************
    DedupStat - Shows an estimation of deduplication advantages for specified block size.
    Copyright (C) 2014 Natalia Portillo

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*******************************************************************************************/

using System;
using System.IO;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace DedupStat
{
    class MainClass
    {
        static Dictionary<string, ulong> hashes;
        static List<string> files;

        public static void Main(string[] args)
        {
            UInt32 blocksize;
            bool verbose = false;
            if (args.Length != 2)
                ShowHelp();
            else if (!UInt32.TryParse(args [0], out blocksize))
                ShowHelp();
            else if (blocksize % 512 != 0)
                ShowHelp();
            else if (!Directory.Exists(args [1]))
                ShowHelp();
            else
            {
                hashes = new Dictionary<string, ulong>();
                ulong blocks = 0;
                ulong overhead = 0;
                ulong totalsize = 0;
                DateTime start, end;

                Console.WriteLine("DedupStat - Shows an estimation of deduplication advantages for specified block size.");
                Console.WriteLine("© 2014 Natalia Portillo");
                Console.WriteLine();
                start = DateTime.Now;
                Console.WriteLine("Searching files...");
                files = new List<string>(Directory.EnumerateFiles(args[1], "*", SearchOption.AllDirectories));
                Console.WriteLine("{0} files found.", files.Count);
                Console.WriteLine("Counting {0} bytes sized blocks for found files.", blocksize);

                List<string> wrongfiles = new List<string>();

                foreach (string filePath in files)
                {
                    if (File.Exists(filePath))
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(filePath);
                            long fileBlocks = (long)Math.Ceiling((double)fi.Length / (double)blocksize); 
                            long fileOverhead = fileBlocks * blocksize - fi.Length;
                            if(verbose)
                                Console.WriteLine("File \"{0}\" is {1} bytes, uses {2} blocks of {3} bytes each, for a total of {4} bytes ({5} overhead bytes)",
                                    filePath, fi.Length, fileBlocks, blocksize, fileBlocks * blocksize, fileOverhead);

                            blocks += (ulong)fileBlocks;
                            overhead += (ulong)fileOverhead;
                            totalsize += (ulong)fi.Length;

                            if(verbose)
                                Console.WriteLine("Calculating block checksums");

                            FileStream fs = File.OpenRead(filePath);

                            byte[] b = new byte[blocksize];
                            int count = 1;
                            int fileUniqueBlocks = 0;
                            int fileDuplicatedBlocks = 0;
                            while (fs.Read(b, 0, (int)blocksize) > 0)
                            {
                                Console.Write("\rCalculating hash of block {0}/{1}", count, fileBlocks);
                                string hash = CalculateSHA1(b);

                                if (hashes.ContainsKey(hash))
                                {
                                    ulong ref_count;
                                    hashes.TryGetValue(hash, out ref_count);
                                    hashes.Remove(hash);
                                    ref_count++;
                                    hashes.Add(hash, ref_count);
                                    fileDuplicatedBlocks++;
                                } else
                                {
                                    hashes.Add(hash, 1);
                                    fileUniqueBlocks++;
                                }

                                count++;
                            }
                            Console.Write("\r                                                                                                ");
                            if(verbose)
                                Console.WriteLine("{0} blocks, {1} unique, {2} duplicated", fileBlocks, fileUniqueBlocks, fileDuplicatedBlocks);

                            fs.Close();
                        }
                        catch (Exception Ex)
                        {
                            if(verbose)
                                Console.WriteLine("Exception \"{0}\" on file \"{1}\"", Ex.Message, filePath);
                            wrongfiles.Add(filePath);
                        }
                    }
                    else
                    {
                        wrongfiles.Add(filePath);
                    }
                }

                foreach (string wrongfile in wrongfiles)
                    files.Remove(wrongfile);

                end = DateTime.Now;

                Console.WriteLine();
                Console.WriteLine("Summary:");
                Console.WriteLine("{0} files for a total of {1} bytes", files.Count, totalsize);
                Console.WriteLine("{0} bytes/block, for a total of {1} blocks used, using {2} bytes", blocksize, blocks, blocksize*blocks);
                Console.WriteLine("{0} wasted bytes (should be {1}, difference is {2})", overhead, (blocks * blocksize) - totalsize, blocks * blocksize - totalsize - overhead);
                Console.WriteLine("{0} unique blocks, using {1} bytes, {2}%", hashes.Count, hashes.Count * blocksize, (double)hashes.Count*100/(double)blocks);
                Console.WriteLine("{0} duplicate blocks, using {1} bytes, {2}%", blocks - (ulong)hashes.Count, (blocks - (ulong)hashes.Count) * blocksize, (double)(blocks - (ulong)hashes.Count)*100/(double)blocks);
                Console.WriteLine("Took {0} seconds, approx. {1} Mb/sec", (end - start).TotalSeconds, totalsize / 1048576 / (end - start).TotalSeconds);
            }

        }

        public static void ShowHelp()
        {
            Console.WriteLine("DedupStat - Shows an estimation of deduplication advantages for specified block size.");
            Console.WriteLine("© 2014 Natalia Portillo");
            Console.WriteLine();
            Console.WriteLine("Usage: dedupstat <block_size> <path>");
            Console.WriteLine("\t<block_size>\tBlock size in bytes, must be multiple of 512");
            Console.WriteLine("\t<path>\tFolder path");
        }

        private static string CalculateSHA1(byte[] block)
        {
            using (SHA1Managed sha1 = new SHA1Managed())
            {
                byte[] hash = sha1.ComputeHash(block);
                StringBuilder formatted = new StringBuilder(2 * hash.Length);
                foreach (byte b in hash)
                {
                    formatted.AppendFormat("{0:X2}", b);
                }

                return formatted.ToString();
            }
        }
    }
}
