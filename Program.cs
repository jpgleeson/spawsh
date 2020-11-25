﻿using System;
using System.Net.Sockets;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace spawsh
{
    class Program
    {

        static string server = "gemini.circumlunar.space";
        static string page = "/";
        static string[] LineBuffer;
        static bool inInteractive = false;
        static int currentLine = 0;
        static int selectedLinkIndex = 0;

        static void Main(string[] args)
        {

            string server = "gemini.circumlunar.space";
            string page = "/";
            bool validProtocol = true;

            int windowLineCount = Console.WindowHeight;
            

            if (args.Length > 0)
            {
                string hostArgument = args[0];

                if (hostArgument == "-i")
                {
                    inInteractive = true;
                    string[] LineBuffer = new string[windowLineCount];

                    while (inInteractive)
                    {
                        interactiveLoop();
                    }
                }
                else
                {
                    if (hostArgument.Contains("gemini://"))
                    {
                        hostArgument = hostArgument.Trim();
                        hostArgument = hostArgument.Substring(9);
                    }
                    else if (hostArgument.Contains("https://") || hostArgument.Contains("http://") || hostArgument.Contains("gopher://"))
                    {
                        Console.WriteLine("Protocol not supported.");
                        validProtocol = false;
                    }

                    if (hostArgument.Contains("/"))
                    {
                        int firstSlashIndex = hostArgument.IndexOf('/');

                        server = hostArgument.Remove(firstSlashIndex);
                        page = hostArgument.Substring(firstSlashIndex, hostArgument.Length - firstSlashIndex);
                    }
                    else
                    {
                        server = hostArgument;
                    }

                    if (validProtocol)
                    {
                        TcpClient client = new TcpClient(server, 1965);


                        using (SslStream sslStream = new SslStream(client.GetStream(), false,
                            new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
                        {
                            sslStream.AuthenticateAsClient(server);

                            byte[] messageToSend = Encoding.UTF8.GetBytes("gemini://" + server + page + '\r' + '\n');
                            sslStream.Write(messageToSend);

                            string responseData = ReadMessage(sslStream);

                            handleResponse(responseData);

                        }
                        client.Close();
                    }
                }
            }
            
        }

        public static bool ValidateServerCertificate(object sender, X509Certificate certificate,
        X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }

        public static void interactiveLoop()
        {
            Console.Clear();
            LineBuffer = fetchPage();

            for (int i = 1; i < LineBuffer.Length; i++)
            {
                Console.WriteLine(LineBuffer[i]);
            }

            for (int i = 0; i < Console.WindowWidth; i++)
            {
                Console.Write("-");
            }
            Console.Write('\n');

            for (int i = 0; i < Console.WindowWidth - LineBuffer[0].Length; i++)
            {
                Console.Write(" ");    
            }
            Console.WriteLine(LineBuffer[0]);

            ConsoleKeyInfo keyRead = Console.ReadKey();

            if (keyRead.Key == ConsoleKey.Escape)
            {
                inInteractive = false;
            }
            else if (keyRead.Key == ConsoleKey.Enter)
            {
                string newInput = Console.ReadLine();
                Console.WriteLine(newInput);
                if (buildRequest(newInput))
                {
                    LineBuffer = fetchPage();
                }
                //server = blah
                //page = blah
                //LineBuffer = fetchPage();
            }
            else if (keyRead.Key == ConsoleKey.DownArrow)
            {
                selectedLinkIndex++;
            }
            else if (keyRead.Key == ConsoleKey.UpArrow)
            {
                selectedLinkIndex--;
            }

            if (selectedLinkIndex < 0)
            {
                selectedLinkIndex = 0;
            }
        }

        public static string[] fetchPage()
        {
            TcpClient client = new TcpClient(server, 1965);
            string responseData;

            using (SslStream sslStream = new SslStream(client.GetStream(), false,
                new RemoteCertificateValidationCallback(ValidateServerCertificate), null))
            {
                sslStream.AuthenticateAsClient(server);

                byte[] messageToSend = Encoding.UTF8.GetBytes("gemini://" + server + page + '\r' + '\n');
                sslStream.Write(messageToSend);

                responseData = ReadMessage(sslStream);

                //handleResponse(responseData);

            }
            client.Close();

            return responseData.Split('\n');
        }

        public static bool buildRequest(string inputString)
        {
            if (inputString.Contains("gemini://"))
            {
                inputString = inputString.Trim();
                inputString = inputString.Substring(9);
            }
            else if (inputString.Contains("https://") || inputString.Contains("http://") || inputString.Contains("gopher://"))
            {
                Console.WriteLine("Protocol not supported.");
                return false;
            }

            if (inputString.Contains("/"))
            {
                int firstSlashIndex = inputString.IndexOf('/');

                server = inputString.Remove(firstSlashIndex);
                page = inputString.Substring(firstSlashIndex, inputString.Length - firstSlashIndex);
            }
            else
            {
                server = inputString;
            }

            return true;
        }

        static string ReadMessage(SslStream sslStream)
        {
            // Read the  message sent by the client.
            // The client signals the end of the message using the
            // "<EOF>" marker.
            byte[] buffer = new byte[2048];
            StringBuilder messageData = new StringBuilder();
            int bytes = -1;
            do
            {
                // Read the client's test message.
                bytes = sslStream.Read(buffer, 0, buffer.Length);

                // Use Decoder class to convert from bytes to UTF8
                // in case a character spans two buffers.
                Decoder decoder = Encoding.UTF8.GetDecoder();
                char[] chars = new char[decoder.GetCharCount(buffer, 0, bytes)];
                decoder.GetChars(buffer, 0, bytes, chars, 0);
                messageData.Append(chars);
                // Check for EOF or an empty message.
                if (messageData.ToString().IndexOf("<EOF>") != -1)
                {
                    break;
                }
            } while (bytes != 0);

            return messageData.ToString();
        }

        static void handleResponse(string responseData)
        {
            string[] responseLines = responseData.Split('\n');
            string responseBody = "";

            string responseHeader = responseLines[0];


            if (responseLines.Length > 1)
            {
                string[] responseBodyLines = new string[responseLines.Length-1];
                for (int i = 1; i < responseLines.Length; i++)
                {
                    responseBodyLines[i - 1] = responseLines[i];
                }
                responseBody = string.Join('\n', responseBodyLines);

            }

            string responseCode = responseHeader.Split(' ')[0];

            if (responseCode == "20")
            {
                Console.WriteLine(responseBody);
            }
            else if (responseCode[0] == '3')
            {
                Console.WriteLine("Redirect to {0}", responseHeader.Split(' ')[1]);
                Console.WriteLine("Try again at new url above.");
            }
            else if (responseCode == "50")
            {
                Console.WriteLine("Permanent Failure");
            }
            else if (responseCode == "51")
            {
                Console.WriteLine("File not found.");
            }
        }

    }
}
