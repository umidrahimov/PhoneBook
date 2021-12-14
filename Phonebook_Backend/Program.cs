using System;

namespace AbbTech
{
    class Program
    {
        static void Main(string[] strings)
        {
            Helper helper = new Helper();
            helper.Start();
            Console.WriteLine($"Server is running on http://+:8080/");
            Console.WriteLine("Press any key to stop HTTP server...");
            Console.ReadLine();
        }

    }
}
