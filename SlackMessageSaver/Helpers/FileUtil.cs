using System;

namespace SlackMessageSaver.Helpers
{
    class FileUtil
    {
        private static readonly Random random = new Random();
        private const string folderPrefix = "dump_";
        public static string GetNewFileNameIfExists(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return Path.GetFileName(filePath);
            }

            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string fileExtension = Path.GetExtension(filePath);
            string directory = Path.GetDirectoryName(filePath);
            int counter = 1;

            string newFileName = $"{fileName} ({counter}){fileExtension}";
            string newPath = Path.Combine(directory, newFileName);

            while (File.Exists(newPath))
            {
                counter++;
                newFileName = $"{fileName} ({counter}){fileExtension}";
                newPath = Path.Combine(directory, newFileName);
            }

            return newFileName;
        }
        public static string GenerateFolderName()
        {
            var timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var randomSuffix = new string(GenerateRandomChars(6));
            return folderPrefix + timeStamp + "_" + randomSuffix;
        }
        private static char[] GenerateRandomChars(int length)
        {
            char[] result = new char[length];
            for (int i = 0; i < length; i++)
            {
                result[i] = (char)random.Next(97, 123); // a-z
            }
            return result;
        }

    }

}
