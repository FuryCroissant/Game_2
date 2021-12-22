using System.Net.Sockets;
using System.Text.Json;
using Lib;

namespace Client;

class Client
{
    static async Task Main(string[] args)
    {
        try
        {
            while (true)
            {
                try
                {
                    await StartClient();
                }
                catch (SocketException)
                {
                    Console.WriteLine("Connection to server failed. Reconnecting in 5 seconds...");
                    Thread.Sleep(5000);
                    continue;
                }

                break;
            }
        }
        catch (IOException)
        {
            Console.WriteLine("Opponent disconnected. Game aborted.");
        }
        catch (JsonException)
        {
            Console.WriteLine("Opponent sent message in unexpected format. Game aborted.");
        }
        catch (Exception)
        {
            Console.WriteLine("Unknown error.");
            throw;
        }
        finally
        {
            Console.ReadLine();
        }
    }

    static async Task StartClient()
    {
        string localAddr = "127.0.0.1";
        var client = new TcpClient();
        while (!client.Connected)
        {
            await client.ConnectAsync(localAddr, port: 1337);
            Thread.Sleep(100);
        }

        NetworkStream stream = client.GetStream();

        Console.WriteLine("The Game is on :) ");

        byte[] buffer = new byte[1024];

        bool continueGame = true;
        bool SeqCreated = false;

        while (continueGame)
        {
            if (SeqCreated)//загадывает клиент
            {
                Console.Write(Phrases.Request);
                string? seq = Console.ReadLine();
                if (seq == null || seq.Length != Phrases.SeqLength ||
                    seq.Any(color => !Phrases.Colors.Contains(char.ToLower(color))))
                {
                    Console.WriteLine(Phrases.Rewrite);
                    continue;
                }
                //отправка с клиента
                Message message = new() { Sequence = seq.ToLower() };
                string messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                //получение ответа с сервера
                Console.WriteLine(Phrases.WaitResult);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                messageJson = Helpers.ReadFromBuffer(buffer);
                Signal? opponentResult = JsonSerializer.Deserialize<Message>(messageJson)?.Signal;

                if (opponentResult == null)
                    throw new JsonException();

                if (opponentResult == Signal.Lost)//клиент выиграл
                {
                    Console.WriteLine(Phrases.Victory);
                    continueGame = false;
                    continue;
                }

                if (opponentResult == Signal.GotItRight)//клиент проиграл
                {
                    Console.WriteLine(Phrases.RightType);
                    SeqCreated = false;
                }
            }
            else//загадывает сервер
            {
                Console.WriteLine(Phrases.WaitSequence);
                //получеие с сервера
                await stream.ReadAsync(buffer, 0, buffer.Length);
                string messageJson = Helpers.ReadFromBuffer(buffer);
                string? sequence = JsonSerializer.Deserialize<Message>(messageJson)?.Sequence;

                if (sequence == null)
                    throw new JsonException();

                Console.WriteLine($"Memorize this seq ({Phrases.MemorizeTime} seconds!): {sequence}");
                Thread.Sleep(Phrases.MemorizeTime * 1000);

                Console.Clear();
                Console.Write(Phrases.RememberType);
                string? recreatedSequence = Console.ReadLine();
                Message message;

                if (recreatedSequence == null || recreatedSequence.ToLower() != sequence)//проигрыш
                {
                    Console.WriteLine(Phrases.Defeat);
                    message = new() { Signal = Signal.Lost };
                    messageJson = JsonSerializer.Serialize(message);
                    Helpers.WriteToBuffer(messageJson, buffer);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    continueGame = false;
                    continue;
                }

                Console.WriteLine(Phrases.RightType);//выигрыш, отправка ответа, смена ролей
                message = new() { Signal = Signal.GotItRight };
                messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                SeqCreated = true;
            }
        }
    }
}