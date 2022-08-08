/*THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.*/

using System.Net.Sockets;
using System.Net;
using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace Celeste.Mod.CelesteRTDataProvider
{
    public class websocketServer
    {
        public NetworkStream stream = null;
        public TcpListener server = null;
        public TcpClient client = null;
        public string lastMessage = "";
        public bool isNewConnection = false;

        public bool IsConnected()
        {
            {
                try
                {
                    if (client != null && client.Client != null && client.Client.Connected)
                    {
                        // Detect if client disconnected
                        if (client.Client.Poll(0, SelectMode.SelectRead))
                        {
                            byte[] buff = new byte[1];
                            if (client.Client.Receive(buff, SocketFlags.Peek) == 0)
                            {
                                // Client disconnected
                                return false;
                            }
                            else
                            {
                                return true;
                            }
                        }

                        return true;
                    }
                    else
                    {
                        return false;
                    }
                }
                catch
                {
                    return false;
                }
            }
        }

        public void reconnectClient()
        {
            client = server.AcceptTcpClient();
            stream = client.GetStream();
            if (lastMessage !=  "")
            {
                isNewConnection = true;
            }    
        }

        public void startServer(int port)
        {
            string ip = "127.0.0.1";
            server = new TcpListener(IPAddress.Parse(ip), port);
            server.Start();
            string swkaSha1Base64 = "";
            byte[] response = new byte[1];
            client = server.AcceptTcpClient();

            stream = client.GetStream();

            while (true)
            {
                try
                {
                    // Do not run logic if client is currently connected and there is no data stream (post-get)
                    while (!stream.DataAvailable && IsConnected());

                    // Automatically handle reconnects
                    if (!IsConnected())
                    {
                        reconnectClient();
                    }

                    byte[] bytes = new byte[client.Available];
                    stream.Read(bytes, 0, client.Available);
                    string s = Encoding.UTF8.GetString(bytes);

                    if (Regex.IsMatch(s, "^GET", RegexOptions.IgnoreCase))
                    {
                        // Required connection logic
                        string swk = Regex.Match(s, "Sec-WebSocket-Key: (.*)").Groups[1].Value.Trim();
                        string swka = swk + "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
                        byte[] swkaSha1 = System.Security.Cryptography.SHA1.Create().ComputeHash(Encoding.UTF8.GetBytes(swka));
                        swkaSha1Base64 = Convert.ToBase64String(swkaSha1);
                        response = Encoding.UTF8.GetBytes(
                            "HTTP/1.1 101 Switching Protocols\r\n" +
                            "Connection: Upgrade\r\n" +
                            "Upgrade: websocket\r\n" +
                            "Sec-WebSocket-Accept: " + swkaSha1Base64 + "\r\n\r\n");
                        stream.Write(response, 0, response.Length);
                        if (isNewConnection)
                        {
                            // Added this block to send cached messages to the client.
                            // If we've already sent data recently, it will send whatever the most recently saved piece is.
                            isNewConnection = false;
                            sendMessage(lastMessage);
                        }
                    }
                }
                catch
                {
                    reconnectClient();
                }

            }
        }

        public void sendMessage(string inputText)
        {
            lastMessage = inputText;
            byte[] sendBytes = Encoding.UTF8.GetBytes(inputText);
            byte lengthHeader = 0;
            byte[] lengthCount = new byte[] { };

            if (sendBytes.Length <= 125)
                lengthHeader = (byte)sendBytes.Length;

            if (125 < sendBytes.Length && sendBytes.Length < 65535) //System.UInt16
            {
                lengthHeader = 126;

                lengthCount = new byte[] {
                    (byte)(sendBytes.Length >> 8),
                    (byte)(sendBytes.Length)
                };
            }

            if (sendBytes.Length > 65535)//max 2_147_483_647 but .Length -> System.Int32
            {
                lengthHeader = 127;
                lengthCount = new byte[] {
                    (byte)(sendBytes.Length >> 56),
                    (byte)(sendBytes.Length >> 48),
                    (byte)(sendBytes.Length >> 40),
                    (byte)(sendBytes.Length >> 32),
                    (byte)(sendBytes.Length >> 24),
                    (byte)(sendBytes.Length >> 16),
                    (byte)(sendBytes.Length >> 8),
                    (byte)sendBytes.Length,
                };
            }

            List<byte> responseArray = new List<byte>() { 0b10000001 };

            responseArray.Add(lengthHeader);
            responseArray.AddRange(lengthCount);
            responseArray.AddRange(sendBytes);

            stream.Write(responseArray.ToArray(), 0, responseArray.Count);
        }
    }
}