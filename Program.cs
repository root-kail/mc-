using Ionic.Zlib;
using Log4cs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace 集群压测
{
    class Program
    {
        static int compression_treshold=0;
        static string host="127.0.0.1";
        static int port=25565;
        static int protocol=754;
        static Logger log = new Logger("压测");
        static byte[] port2;
        static int threads=100;
        static int delay=10;
        static string proxy;
        static int delay2=1000;
        static int keep_alive_client=16;
        static int keep_alive_server=31;
        static byte[] pack_to_send;

        static int rep = 0;
        static int next_state = 2;
        public static byte[] GetString(string text)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return ConcatBytes(GetVarInt(bytes.Length), bytes);
        }
        public static byte[] GetVarInt(int paramInt)
        {
            List<byte> bytes = new List<byte>();
            while ((paramInt & -128) != 0)
            {
                bytes.Add((byte)(paramInt & 127 | 128));
                paramInt = (int)(((uint)paramInt) >> 7);
            }
            bytes.Add((byte)paramInt);
            return bytes.ToArray();
        }
        public static byte[] ConcatBytes(params byte[][] bytes)
        {
            List<byte> result = new List<byte>();
            foreach (byte[] array in bytes)
                result.AddRange(array);
            return result.ToArray();
        }
        public static byte[] GetUShort(ushort number)
        {
            byte[] theShort = BitConverter.GetBytes(number);
            Array.Reverse(theShort);
            return theShort;
        }
        private static string GenerateCheckCode(int codeCount)
        {
            string str = string.Empty;
            long num2 = DateTime.Now.Ticks + rep;
            rep++;
            Random random = new Random(((int)(((ulong)num2) & 0xffffffffL)) | ((int)(num2 >> rep)));
            for (int i = 0; i < codeCount; i++)
            {
                char ch;
                int num = random.Next();
                if ((num % 2) == 0)
                {
                    ch = (char)(0x30 + ((ushort)(num % 10)));
                }
                else
                {
                    ch = (char)(0x41 + ((ushort)(num % 0x1a)));
                }
                str = str + ch.ToString();
            }
            return str;
        }
        static bool useProxy=false;
        static int disconnect=25;
        static bool keepAlive = true;
        static void Main(string[] args)
        {
            
            log.Debug("Minecraft假人压测-V1.0-BC支持");
            log.Debug("Made by NetherStudio");
            Console.WriteLine("=====================================");
            host=log.GetInput("ip:");
            TryParse(log.GetInput("Port(25565):"),25565,out port);
            log.Info("正在写入配置文件");
            proxy = "listeners:\n- query_port: 25577\n  motd: '&1Another Bungee server'\n  tab_list: GLOBAL_PING\n  query_enabled: false\n  proxy_protocol: false\n  forced_hosts:\n    pvp.md-5.net: pvp\n  ping_passthrough: false\n  priorities:\n  - lobby\n  bind_local_address: true\n  host: 127.0.0.1:25577\n  max_players: 1000\n  tab_size: 60\n  force_default_server: false\nremote_ping_cache: -1\nnetwork_compression_threshold: 256\npermissions:\n  default:\n  - bungeecord.command.server\n  - bungeecord.command.list\n  admin:\n  - bungeecord.command.alert\n  - bungeecord.command.end\n  - bungeecord.command.ip\n  - bungeecord.command.reload\nlog_pings: true\nconnection_throttle_limit: 3\nserver_connect_timeout: 5000\ntimeout: 30000\nstats: 6c09f2af-ff88-4d72-a670-ca62f29db597\nplayer_limit: -1\nip_forward: false\ngroups:\n  md_5:\n  - admin\nremote_ping_timeout: 5000\nconnection_throttle: 4000\nlog_commands: false\nprevent_proxy_connections: false\nonline_mode: false\nforge_support: false\ndisabled_commands:\n- disabledcommandhere\nservers:\n  lobby:\n    motd: '&1Just another BungeeCord - Forced Host'\n    address: " + host + ":" + port + "\n    restricted: false";
            StreamWriter config = new StreamWriter("config.yml");
            config.WriteLine(proxy);
            config.Flush();
            config.Close();
            config.Dispose();
            TryParse(log.GetInput("protocol(754):"),754,out protocol);
            TryParse(log.GetInput("线程数(100):"),100,out threads);
            TryParse(log.GetInput("next_state(2):"),2,out next_state);
            TryParse(log.GetInput("连接状态检查间隔(10)(ms):"),10,out delay);
            TryParse(log.GetInput("加入服务器间隔(1000)(ms):"),1000,out delay2);
            TryParse(log.GetInput("keep_alive(client)(16):"),16,out keep_alive_client);
            TryParse(log.GetInput("keep_alive(server)(37):"),37,out keep_alive_server);
            TryParse(log.GetInput("Disconnect_Packet(25):"),25,out disconnect);
            TryParse(log.GetInput("compression_treshold(0):"),0,out compression_treshold);
            pack_to_send = parsePacket(log.GetInput("进服后发送的数据包({}):"));
            useProxy = log.GetInput("是否启用代理(false):")=="true";
            keepAlive = log.GetInput("是否接收keepAlive(true):") != "false";
            Console.WriteLine("=====================================");

            log.Info("准备压测"+host+":"+port);
            if (useProxy)
            {
                log.Info("正在开启代理(请确保你安装了java)");
                string filename = "cmd";
                string dir = Environment.CurrentDirectory;
                Process.Start(filename, "/k \"cd " + dir + " && java -jar BungeeCord.jar -noconsole\"");
                log.Info("等待代理启动");
                proxy_test();
                log.Info("启动成功!开始攻击!");
                port2 = GetUShort((ushort)25577);
                host = "127.0.0.1";
                port = 25577;
            }
            else
            {
                port2 = GetUShort((ushort)port);
            }
            for (int i = threads; i > 0; i--)
            {
                new Thread(new ThreadStart(Run)).Start();
            }


        }
        public static void TryParse(string s, int default_, out int result)
        {
            int e;
            if (s != "")
            {
                if(int.TryParse(s,out e))
                {
                    result = e;
                }
                else
                {
                    result = default_;
                }
            }
            else
            {
                result = default_;
            }
        }
        public static void proxy_test()
        {
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            while (!sock.Connected)
            {
                try
                {
                    sock.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 25577));
                    Thread.Sleep(1000);
                }
                catch
                {
                }
            }
        }
        public static byte[] parsePacket(string packet)
        {
            if (packet.StartsWith("{") && packet.EndsWith("}"))
            {
                packet=packet.Substring(1, packet.Length - 2);
                string[] pkt = packet.Split(',');
                Queue<byte> pack = new Queue<byte>();
                foreach(string str in pkt)
                {
                    byte b=0;
                    if(byte.TryParse(str, out b))
                    {
                        pack.Enqueue(b);
                    }
                }
                byte id = pack.Dequeue();
                return getPacket((int)id, pack.ToArray(),compression_treshold);

            }
            else
            {
                return new byte[] { };
            }
        }
        public static byte[] Compress(byte[] to_compress)
        {
            byte[] data;
            using (System.IO.MemoryStream memstream = new System.IO.MemoryStream())
            {
                using (ZlibStream stream = new ZlibStream(memstream, 0))
                {
                    stream.Write(to_compress, 0, to_compress.Length);
                }
                data = memstream.ToArray();
            }
            return data;
        }
        public static byte[] getPacket(int packetID, IEnumerable<byte> packetData,int compression_treshold)
        {
            //The inner packet
            byte[] the_packet = ConcatBytes(GetVarInt(packetID), packetData.ToArray());
            if (compression_treshold > 0) //Compression enabled?
            {
                if (the_packet.Length >= compression_treshold) //Packet long enough for compressing?
                {
                    byte[] compressed_packet = Compress(the_packet);
                    the_packet = ConcatBytes(GetVarInt(the_packet.Length), compressed_packet);
                }
                else
                {
                    byte[] uncompressed_length = GetVarInt(0); //Not compressed (short packet)
                    the_packet = ConcatBytes(uncompressed_length, the_packet);
                }
            }
            return ConcatBytes(GetVarInt(the_packet.Length), the_packet);
        }
        public static int ReadNextVarInt(Queue<byte> cache)
        {
            string rawData = BitConverter.ToString(cache.ToArray());
            int i = 0;
            int j = 0;
            int k = 0;
            while (true)
            {
                k = ReadNextByte(cache);
                i |= (k & 0x7F) << j++ * 7;
                //if (j > 5) throw new OverflowException("VarInt too big " + rawData);
                if ((k & 0x80) != 128) break;
            }
            return i;
        }
        public static byte ReadNextByte(Queue<byte> cache)
        {
            byte result = cache.Dequeue();
            return result;
        }
        public static void OnPacket(byte[] buffer,int len,Socket socket,string name)
        {
            if (socket.Connected)
            {

                Socket sock = socket;
                Queue<byte> packetData = new Queue<byte>();
                byte[] receive = buffer;
                int l = len;
                int id = receive[0];
                int size = len; //Packet size
                byte[] rawpacket = receive; //Packet contents
                for (int i = 0; i < rawpacket.Length; i++)
                    packetData.Enqueue(rawpacket[i]);

                //Handle packet decompression
                if (compression_treshold > 0)
                {
                    int sizeUncompressed = ReadNextVarInt(packetData);
                    if (sizeUncompressed != 0) // != 0 means compressed, let's decompress
                    {
                        byte[] toDecompress = packetData.ToArray();
                        byte[] uncompressed = ZlibUtils.Decompress(toDecompress, sizeUncompressed);
                        packetData.Clear();
                        for (int i = 0; i < uncompressed.Length; i++)
                            packetData.Enqueue(uncompressed[i]);
                    }
                }
                receive = packetData.ToArray();
                if (id == keep_alive_server)
                {
                    if (keepAlive)
                    {
                        Queue<byte> data = new Queue<byte>(receive);
                        data.Dequeue();
                        Queue<byte> thingToSend = new Queue<byte>();
                        thingToSend.Enqueue((byte)keep_alive_client);
                        foreach (byte b in data)
                        {
                            thingToSend.Enqueue(b);
                        }
                        byte[] the_packet = ConcatBytes(thingToSend.ToArray());
                        if (compression_treshold > 0) //Compression enabled?
                        {
                            if (the_packet.Length >= compression_treshold) //Packet long enough for compressing?
                            {
                                byte[] compressed_packet = Compress(the_packet);
                                the_packet = ConcatBytes(GetVarInt(the_packet.Length), compressed_packet);
                            }
                            else
                            {
                                byte[] uncompressed_length = GetVarInt(0); //Not compressed (short packet)
                                the_packet = ConcatBytes(uncompressed_length, the_packet);
                            }
                        }
                        sock.Send(ConcatBytes(GetVarInt(the_packet.Length), the_packet));
                    }
                    string info = "[" + name + "]Keep alive";
                    if (!keepAlive)
                    {
                        info = info + ",but no request.";
                    }
                    log.Info(info);
                }
                if (id == 0x03)
                {
                    Queue<byte> data = new Queue<byte>(receive);
                    data.Dequeue();
                    compression_treshold = ReadNextVarInt(data);
                    log.Info("Set compression_treshold to " + compression_treshold);
                }
                if (id == disconnect)
                {
                    Queue<byte> data = new Queue<byte>(receive);
                    data.Dequeue();
                    Console.WriteLine("[Info][" + name + "]从服务器断开连接");
                    sock.Disconnect(false);
                    sock.Close();
                    sock.Dispose();
                }
                
            }
        }
        public static bool is_server_online = true;
        public static void Run()
        {
            while (true)
            {
                if (is_server_online)
                {
                    string name = GenerateCheckCode(new Random().Next(5, 15));
                    try
                    {
                        byte[] handshake_packet = ConcatBytes(GetVarInt(protocol), GetString(host), port2, GetVarInt(next_state));
                        Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sock.Connect(new IPEndPoint(IPAddress.Parse(host), port));
                        sock.ReceiveTimeout = int.MaxValue;
                        sock.Send(getPacket(0x00, handshake_packet, 0));
                        if (next_state == 2)
                        {
                            byte[] login_packet = GetString(name);
                            sock.Send(getPacket(0x00, login_packet, 0));
                            log.Info(name + "连接成功");
                        }
                        while (sock.Connected)
                        {
                            if (next_state == 2)
                            {
                                byte[] receive = new byte[1024];
                                int l = sock.Receive(receive);
                                OnPacket(receive, l, sock, name);
                                Thread.Sleep(delay);
                                sock.Send(pack_to_send);
                                sock.Send(getPacket(0x04, new byte[] { 0 }, compression_treshold));
                            }
                            else
                            {
                                sock.Send(getPacket(0x00, handshake_packet, compression_treshold));
                            }
                        }
                        Thread.Sleep(delay2);
                        sock.Disconnect(false);
                        sock.Close();
                        sock.Dispose();
                        Console.WriteLine("[Info][Thread]" + name + "因连接异常断开连接");
                    }
                    catch (SocketException e)
                    {
                        Console.WriteLine("[Info][Thread]" + name + "因发生错误断开连接:" + e.Message + "(错误代码" + e.ErrorCode + ")");
                        if (e.ErrorCode == 10060)
                        {
                            log.Info("检测到服务器可能没有开启,线程进入等待状态");
                            is_server_online = false;
                        }
                        new Thread(new ThreadStart(Run)).Start();
                        Thread.CurrentThread.Abort();
                    }
                    catch (ObjectDisposedException)
                    {
                        Console.WriteLine("[Info][Thread]" + name + "已断开连接");
                        new Thread(new ThreadStart(Run)).Start();
                        Thread.CurrentThread.Abort();
                    }
                }
                else
                {
                    try
                    {
                        string name = GenerateCheckCode(new Random().Next(5, 15));
                        byte[] handshake_packet = ConcatBytes(GetVarInt(protocol), GetString(host), port2, GetVarInt(next_state));
                        Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        sock.Connect(new IPEndPoint(IPAddress.Parse(host), port));
                        sock.ReceiveTimeout = int.MaxValue;
                        sock.Send(getPacket(0x00, handshake_packet, 0));
                        byte[] login_packet = GetString(name);
                        sock.Send(getPacket(0x00, login_packet, 0));
                        if (sock.Connected)
                        {
                            log.Info("检测到服务器开启,恢复攻击状态");
                            is_server_online = true;
                            sock.Disconnect(false);
                            sock.Dispose();
                        }
                    }
                    catch (SocketException)
                    {

                    }
                    catch (ObjectDisposedException)
                    {

                    }
                }
            }
        }
    }
}
