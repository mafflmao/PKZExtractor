using System;
using System.Diagnostics;
using System.IO;
using System.Text;

class PKZExtractor
{
    static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: PKZExtractor <path to decompressed .pkz file>");
            return;
        }

        string filePath = args[0];
        if (!File.Exists(filePath))
        {
            Console.WriteLine("File not found: " + filePath);
            return;
        }

        string directoryName = Path.GetFileNameWithoutExtension(filePath);

        try
        {

            string outputDirectory = Path.Combine(Path.GetDirectoryName(filePath), directoryName);
            if (!Directory.Exists(outputDirectory))
            {
                Directory.CreateDirectory(outputDirectory);
            }

            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            using (BinaryReader reader = new BinaryReader(fs))
            {
                byte[] headerToFind = { 0x52, 0x49, 0x46, 0x58 };
                long headerPosition;
                int fileCounter = 0;

                while (fs.Position < fs.Length - 4)
                {
                    headerPosition = FindHeader(reader, fs, headerToFind);

                    if (headerPosition != -1)
                    {

                        string fileName = GetFileName(reader, fs, headerPosition);
                        if (string.IsNullOrEmpty(fileName))
                        {
                            fileName = "Untitled.wem";
                        }

                        Console.WriteLine("Audio Header found at position: 0x" + headerPosition.ToString("X"));
                        Console.WriteLine("Extracting file: " + fileName);

                        long endPosition = FindEndingBytes(reader, fs, headerPosition);

                        if (endPosition != -1)
                        {
                            ExportWemFile(filePath, outputDirectory, fileName, headerPosition, endPosition);
                            Console.WriteLine("File saved as: " + fileName);
                            fileCounter++;
                        }
                        else
                        {
                            Console.WriteLine("No proper ending bytes found after the header at 0x" + headerPosition.ToString("X"));
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (fileCounter > 0)
                {
                    ConvertWemFilesToWav(outputDirectory, directoryName);
                    Console.WriteLine($"{fileCounter} files were extracted and converted.");
                }
                else
                {
                    Console.WriteLine("No files were extracted.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("An error occurred: " + ex.Message);
        }
    }

    static long FindHeader(BinaryReader reader, FileStream fs, byte[] headerToFind)
    {
        while (fs.Position < fs.Length - 4)
        {
            if (reader.ReadByte() == headerToFind[0])
            {
                long currentPosition = fs.Position;
                byte[] potentialHeader = reader.ReadBytes(3);

                if (potentialHeader.Length == 3 &&
                    potentialHeader[0] == headerToFind[1] &&
                    potentialHeader[1] == headerToFind[2] &&
                    potentialHeader[2] == headerToFind[3])
                {
                    return currentPosition - 1;
                }

                fs.Position = currentPosition;
            }
        }

        return -1;
    }

    static string GetFileName(BinaryReader reader, FileStream fs, long headerPosition)
    {
        fs.Position = headerPosition - 1;

        StringBuilder fileNameBuilder = new StringBuilder();
        bool readingWord = false;
        long position = headerPosition - 1;
        while (position >= 0)
        {
            fs.Position = position;
            byte b = reader.ReadByte();
            char c = (char)b;

            if (char.IsLetterOrDigit(c) || c == '_')
            {
                fileNameBuilder.Insert(0, c);
                readingWord = true;
            }
            else
            {
                if (readingWord)
                {

                    string candidate = fileNameBuilder.ToString();
                    if (IsValidFileName(candidate))
                    {
                        return candidate + ".wem";
                    }

                    fileNameBuilder.Clear();
                    readingWord = false;
                }
            }

            position--;
        }

        return "Untitled.wem";
    }

    static bool IsValidFileName(string candidate)
    {

        return !string.IsNullOrEmpty(candidate) && candidate.Contains("_");
    }

    static long FindEndingBytes(BinaryReader reader, FileStream fs, long headerPosition)
    {
        while (fs.Position < fs.Length - 4)
        {
            byte[] sequence = reader.ReadBytes(4);

            if (sequence[0] == 0x00 && sequence[1] == 0x00 && sequence[2] == 0x00 && sequence[3] == 0x00)
            {
                long sequencePosition = fs.Position - 4;

                if (sequencePosition >= headerPosition + 0xDD)
                {
                    return sequencePosition;
                }
            }
        }

        return -1;
    }

    static void ExportWemFile(string filePath, string outputDirectory, string fileName, long start, long end)
    {
        string outputFilePath = Path.Combine(outputDirectory, fileName);

        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader reader = new BinaryReader(fs))
        using (FileStream outFile = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write))
        {
            fs.Position = start;
            byte[] buffer = new byte[end - start];
            reader.Read(buffer, 0, buffer.Length);
            outFile.Write(buffer, 0, buffer.Length);
        }
    }

    static void ConvertWemFilesToWav(string directory, string pkzFileName)
    {
        string vgmstreamPath = "ext-tools/vgmstream-cli.exe";
        foreach (string wemFile in Directory.GetFiles(directory, "*.wem"))
        {
            string wavFile = Path.ChangeExtension(wemFile, ".wav");
            string arguments = $"\"{wavFile}\" \"{wemFile}\"";

            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = vgmstreamPath,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(psi))
            {
                process.WaitForExit();
                if (process.ExitCode == 0)
                {
                    Console.WriteLine($"Converted {Path.GetFileName(wemFile)} to {Path.GetFileName(wavFile)}");
                    File.Delete(wemFile);
                }
                else
                {
                    Console.WriteLine($"Error converting {Path.GetFileName(wemFile)} to .wav");
                }
            }
        }
    }
}