using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Lib;

namespace Server;

class Server
{
    static async Task Main(string[] args)
    {
        try
        {
            await StartServer();
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
            Console.WriteLine("Unknown error occured");
            throw;
        }
        finally
        {
            Console.ReadLine();
        }
    }

    static async Task StartServer()
    {
        IPAddress localAddr = IPAddress.Parse("127.0.0.1");
        var server = new TcpListener(localAddr, port: 1337);
        server.Start();
        TcpClient client = await server.AcceptTcpClientAsync();
        server.Stop();
        NetworkStream stream = client.GetStream();

        Console.WriteLine("The Game is on :) ");

        byte[] buffer = new byte[1024];

        bool continueGame = true;
        bool isCreatingSequence = true;

        while (continueGame)
        {
            if (isCreatingSequence)
            {
                Console.Write(ConstantValues.Request);
                string? seq = Console.ReadLine();
                if (seq == null || seq.Length != ConstantValues.SeqLength ||
                    seq.Any(color => !ConstantValues.AvailableColors.Contains(char.ToLower(color))))
                {
                    Console.WriteLine(ConstantValues.Rewrite);
                    continue;
                }

                Message message = new() { Sequence = seq.ToLower() };
                string messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);

                Console.WriteLine(ConstantValues.WaitResult);
                await stream.ReadAsync(buffer, 0, buffer.Length);
                messageJson = Helpers.ReadFromBuffer(buffer);
                Signal? opponentResult = JsonSerializer.Deserialize<Message>(messageJson)?.Signal;

                if (opponentResult == null)
                    throw new JsonException();

                if (opponentResult == Signal.Lost)
                {
                    Console.WriteLine(ConstantValues.Victory);
                    continueGame = false;
                    continue;
                }

                if (opponentResult == Signal.GotItRight)
                {
                    Console.WriteLine(ConstantValues.RightType);
                    isCreatingSequence = false;
                }
            }
            else
            {
                Console.WriteLine(ConstantValues.WaitSequence);//ожидание ввода от другого

                await stream.ReadAsync(buffer, 0, buffer.Length);
                string messageJson = Helpers.ReadFromBuffer(buffer);
                string? sequence = JsonSerializer.Deserialize<Message>(messageJson)?.Sequence;

                if (sequence == null)
                    throw new JsonException();

                Console.WriteLine($"Memorize this seq ({ConstantValues.MemorizeTime} seconds!): {sequence}");//показ посл-ти
                Thread.Sleep(ConstantValues.MemorizeTime * 1000);

                Console.Clear();
                Console.Write(ConstantValues.RememberType);
                string? recreatedSequence = Console.ReadLine();
                Message message;

                if (recreatedSequence == null || recreatedSequence.ToLower() != sequence)
                {
                    Console.WriteLine(ConstantValues.Defeat);
                    message = new() { Signal = Signal.Lost };
                    messageJson = JsonSerializer.Serialize(message);
                    Helpers.WriteToBuffer(messageJson, buffer);
                    await stream.WriteAsync(buffer, 0, buffer.Length);
                    continueGame = false;
                    continue;
                }

                Console.WriteLine(ConstantValues.RightType);
                message = new() { Signal = Signal.GotItRight };
                messageJson = JsonSerializer.Serialize(message);
                Helpers.WriteToBuffer(messageJson, buffer);
                await stream.WriteAsync(buffer, 0, buffer.Length);
                isCreatingSequence = true;
            }
        }
    }
}