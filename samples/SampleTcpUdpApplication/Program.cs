using System.CommandLine;
using System.Net.Sockets;
using System.Net;
using System.Text;
using SampleTcpUdpApplication;

var cts = new CancellationTokenSource();
Console.CancelKeyPress += (s, e) =>
{
    Console.WriteLine("Canceling...");
    cts.Cancel();
    e.Cancel = true;
};

var serverPortOption = new Option<ushort>(
            name: "--port",
            description: "Specifies the port number that application should expose");

var clientPortOption = new Option<ushort>(
            name: "--port",
            description: "Specifies the port number of the server");

var protocolOption = new Option<ProtocolsEnum>(
            name: "--protocol",
            description: "Specifies the protocol");

var serverCommand = new Command("server", "Starts application as a server and exposes the specified port")
{
    protocolOption,
    serverPortOption
};

var clientCommand = new Command("client", "Starts application as a client and connects to the specified port")
{
    protocolOption,
    clientPortOption
};

var rootCommand = new RootCommand("This is a sample application that is used to start a server or work as a client.");
rootCommand.AddCommand(serverCommand);
rootCommand.AddCommand(clientCommand);

serverCommand.SetHandler(async (protocol, port) =>
{
    await StartServerAsync(port, protocol, cts.Token);
}, protocolOption, serverPortOption);

clientCommand.SetHandler(async (protocol, port) =>
{
    await StartClientAsync(port, protocol, cts.Token);   
}, protocolOption, clientPortOption);

return await rootCommand.InvokeAsync(args);

async Task StartServerAsync(ushort port, ProtocolsEnum protocol, CancellationToken cancellationToken)
{
    switch (protocol)
    {
        case ProtocolsEnum.TCP:
            await StartTcpServerAsync(port, cancellationToken);
            break;
        case ProtocolsEnum.UDP:
            await StartUdpServerAsync(port, cancellationToken);
            break;
    }
}

async Task StartClientAsync(ushort port, object protocol, CancellationToken cancellationToken)
{
    switch (protocol)
    {
        case ProtocolsEnum.TCP:
            await StartTcpClientAsync(port, cancellationToken);
            break;
        case ProtocolsEnum.UDP:
            await StartUdpClientAsync(port, cancellationToken);
            break;
    }
}

async Task StartTcpServerAsync(ushort port, CancellationToken cancellationToken)
{
    TcpListener listener = new TcpListener(IPAddress.Any, port);
    listener.Start();

    Console.WriteLine("TCP server is listening on port {0}...", port);

    while (!cancellationToken.IsCancellationRequested)
    {
        try
        {
            TcpClient client = await listener.AcceptTcpClientAsync(cancellationToken);
            Console.WriteLine("Client connected!");

            var task = Task.Run(async () =>
            {
                using (NetworkStream stream = client.GetStream())
                {
                    byte[] buffer = new byte[1024];
                    int bytesRead;

                    while (true)
                    {
                        try
                        {
                            bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);

                            if (bytesRead == 0)
                            {
                                Console.WriteLine("Client disconnected.");
                                break;
                            }

                            string receivedMessage = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                            Console.WriteLine("Received: " + receivedMessage);

                            string responseMessage = $"Hello {receivedMessage.Trim()}";
                            byte[] responseBytes = Encoding.ASCII.GetBytes(responseMessage);
                            await stream.WriteAsync(responseBytes, 0, responseBytes.Length, cancellationToken);
                            Console.WriteLine("Sent: " + responseMessage);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine("Error: " + ex.Message);
                            break;
                        }
                    }
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error accepting client connection: " + ex.Message);
        }
    }

    listener.Stop();

    Console.WriteLine("Server closed.");
}

async Task StartUdpServerAsync(ushort port, CancellationToken cancellationToken)
{
    using (UdpClient udpListener = new UdpClient(port))
    {
        Console.WriteLine("UDP server is listening on port {0}...", port);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                IPEndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, port);
                UdpReceiveResult receiveResult = await udpListener.ReceiveAsync(cancellationToken);

                string receivedMessage = Encoding.ASCII.GetString(receiveResult.Buffer);
                Console.WriteLine("Received from {0}:{1}: {2}", receiveResult.RemoteEndPoint.Address, receiveResult.RemoteEndPoint.Port, receivedMessage);

                string responseMessage = "Hello " + receivedMessage;
                byte[] responseBytes = Encoding.ASCII.GetBytes(responseMessage);

                // Send the response to the client asynchronously
                await udpListener.SendAsync(new ArraySegment<byte>(responseBytes), receiveResult.RemoteEndPoint, cancellationToken);
                Console.WriteLine("Sent to {0}:{1}: {2}", receiveResult.RemoteEndPoint.Address, receiveResult.RemoteEndPoint.Port, responseMessage);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}

async Task StartTcpClientAsync(ushort port, CancellationToken cancellationToken)
{
    try
    {
        using (TcpClient client = new TcpClient("127.0.0.1", port))
        {
            Console.WriteLine("Connected to server at {0}:{1}", "127.0.0.1", port);

            using (NetworkStream stream = client.GetStream())
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    Console.Write("Enter a message (q! to quit): ");
                    string messageToSend = Console.ReadLine();
                    if (messageToSend == "q!")
                        break;

                    byte[] messageBytes = Encoding.ASCII.GetBytes(messageToSend);

                    // Send the message to the server asynchronously
                    await stream.WriteAsync(messageBytes, 0, messageBytes.Length, cancellationToken);
                    Console.WriteLine("Sent: " + messageToSend);

                    // Receive and display the server's response asynchronously
                    byte[] buffer = new byte[1024];
                    int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
                    string serverResponse = Encoding.ASCII.GetString(buffer, 0, bytesRead);
                    Console.WriteLine("Received: " + serverResponse);
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error: " + ex.Message);
    }
}

async Task StartUdpClientAsync(ushort port, CancellationToken cancellationToken)
{
    try
    {
        UdpClient udpClient = new UdpClient();
        IPEndPoint serverEndPoint = new IPEndPoint(IPAddress.Parse("127.0.0.1"), port);

        while (!cancellationToken.IsCancellationRequested)
        {
            Console.Write("Enter a message (q! to quit): ");
            string messageToSend = Console.ReadLine();
            if (messageToSend == "q!")
                break;

            byte[] messageBytes = Encoding.ASCII.GetBytes(messageToSend);

            // Send the message to the server asynchronously
            await udpClient.SendAsync(new ArraySegment<byte>(messageBytes), serverEndPoint, cancellationToken);

            Console.WriteLine("Sent to {0}:{1}: {2}", "127.0.0.1", port, messageToSend);

            // Receive the server's response asynchronously
            UdpReceiveResult receiveResult = await udpClient.ReceiveAsync(cancellationToken);
            string serverResponse = Encoding.ASCII.GetString(receiveResult.Buffer);
            Console.WriteLine("Received from {0}:{1}: {2}", "127.0.0.1", port, serverResponse);
        }

        udpClient.Close();
    }
    catch (Exception ex)
    {
        Console.WriteLine("Error: " + ex.Message);
    }
}
