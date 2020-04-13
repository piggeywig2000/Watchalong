using System;
using System.Collections.Generic;
using System.Text;

namespace Watchalong.Utils
{
    /// <summary>
    /// The type of a debug message. Can be Info, Ok, Warning, Error or Fatal
    /// </summary>
    public enum LogType { Info, Ok, Warning, Error, Fatal }

    public static class ConLog
    {
        /// <summary>
        /// Sends a new debug message to the console. If the type is Fatal, the program will exit
        /// </summary>
        /// <param name="subCategory">The category of the message</param>
        /// <param name="message">The message to send</param>
        /// <param name="type">The severity of the message. If it's Fatal, the program will exit</param>
        public static void Log(string subCategory, string message, LogType type)
        {
            int padAmount = 1;

            Console.ResetColor();
            Console.Write("[");

            switch (type)
            {
                case LogType.Info:
                    padAmount = 4;
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write("INFO");
                    break;
                case LogType.Ok:
                    padAmount = 6;
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.Write("OK");
                    break;
                case LogType.Warning:
                    padAmount = 1;
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.Write("WARNING");
                    break;
                case LogType.Error:
                    padAmount = 3;
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.Write("ERROR");
                    break;
                case LogType.Fatal:
                    padAmount = 3;
                    Console.ResetColor();
                    Console.ForegroundColor = ConsoleColor.DarkRed;
                    Console.Write("FATAL");
                    break;
            }
            
            Console.ResetColor();
            Console.WriteLine("]" + new string(' ', padAmount) + subCategory.ToUpper() + ":" + new string(' ', 20 - subCategory.Length) + message);
            if (type == LogType.Fatal)
            {
                Console.ResetColor();
                Console.WriteLine("\nProgram is terminating...");
                Environment.Exit(1);
            }
        }
    }
}
