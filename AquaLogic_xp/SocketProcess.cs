using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.IO;
using System.Threading;
using System.Net.NetworkInformation;
using System.Net;

namespace AquaLogic_xp
{
    class SocketProcess
    {
        public enum Keys
        {
            //"""Key events which can be sent to the unit"""
            //# Second word is the same on first down, 0000 every 100ms while holding
            RESET = 0x00000000,
            RIGHT = 0x00000001,
            MENU = 0x00000002,
            LEFT = 0x00000004,
            LRBTN = 0x00000005,
            SERVICE = 0x00000008,
            MINUS = 0x00000010,
            PLUS = 0x00000020,
            POOL_SPA = 0x00000040,
            FILTER = 0x00000080,
            LIGHTS = 0x00000100,
            AUX_1 = 0x00000200,
            AUX_2 = 0x00000400,
            AUX_3 = 0x00000800,
            AUX_4 = 0x00001000,
            AUX_5 = 0x00002000,
            AUX_6 = 0x00004000,
            AUX_7 = 0x00008000,
            VALVE_3 = 0x00010000,
            VALVE_4 = 0x00020000,
            HEATER_1 = 0x00040000,
            AUX_8 = 0x00080000,
            AUX_9 = 0x00100000,
            AUX_10 = 0x00200000,
            AUX_11 = 0x00400000,
            AUX_12 = 0x00800000,
            AUX_13 = 0x01000000,
            AUX_14 = 0x02000000,
        }

        [Flags]
        public enum States
        {
            HEATER_1 = 1 << 0,
            VALVE_3 = 1 << 1,
            CHECK_SYSTEM = 1 << 2,
            POOL = 1 << 3,
            SPA = 1 << 4,
            FILTER = 1 << 5,
            LIGHTS = 1 << 6,
            AUX_1 = 1 << 7,
            AUX_2 = 1 << 8,
            SERVICE = 1 << 9,
            AUX_3 = 1 << 10,
            AUX_4 = 1 << 11,
            AUX_5 = 1 << 12,
            AUX_6 = 1 << 13,
            VALVE_4 = 1 << 14,
            SPILLOVER = 1 << 15,
            SYSTEM_OFF = 1 << 16,
            AUX_7 = 1 << 17,
            AUX_8 = 1 << 18,
            AUX_9 = 1 << 19,
            AUX_10 = 1 << 20,
            AUX_11 = 1 << 21,
            AUX_12 = 1 << 22,
            AUX_13 = 1 << 23,
            AUX_14 = 1 << 24,
            SUPER_CHLORINATE = 1 << 25,
            HEATER_AUTO_MODE = 1 << 30, // This is a kludge for the heater auto mode
            FILTER_LOW_SPEED = 1 << 31  //This is a kludge for the low-speed filter
        }

        public class SocketData
        {
            public string DisplayText { get; set; }
            public States Status { get; set; }
            public States Blink { get; set; }
            public string LogText { get; set; }
            public bool Valid { get; set; }
        }

        public bool Connected
        {
            get
            {
                if (_tcpClient != null)
                {
                    return _tcpClient.Connected;
                }
                else
                {
                    return false;
                }
            }
        }

        private bool _menu_locked;

        private readonly TcpClient _tcpClient;
        private readonly NetworkStream _netStream;

        private const byte _FRAME_DLE = 0x10;
        private const byte _FRAME_STX = 0x02;
        private const byte _FRAME_ETX = 0x03;

        // private const byte _WIRELESS_KEY_EVENT = 0x83;
        private const byte _WIRED_LOCAL_KEY_EVENT = 0x02;
        //private const byte _WIRED_REMOTE_KEY_EVENT = 0x03;

        private long _cTick;
        private long _lTick;
        private int _airT;
        private int _poolT = 0;

        public SocketProcess(string ipAddr, int portNum)
        {
            try
            {
                _tcpClient = new();
                _tcpClient.Connect(ipAddr.Trim(), portNum);
                _tcpClient.NoDelay = true;
                _tcpClient.ReceiveTimeout = 5000;
                _tcpClient.SendTimeout = 1000;
                _netStream = _tcpClient.GetStream();
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
        }

        public void Close()
        {
            _netStream.Close();
            _tcpClient.Close();
        }

        public static Keys GetKey(string key)
        {
            return key switch
            {
                "Service" => Keys.SERVICE,
                "Pool" => Keys.POOL_SPA,
                "Spa" => Keys.POOL_SPA,
                "Spillover" => Keys.POOL_SPA,
                "Filter" => Keys.FILTER,
                "Lights" => Keys.LIGHTS,
                "Heater1" => Keys.HEATER_1,
                "Valve3" => Keys.VALVE_3,
                "Valve4" => Keys.VALVE_4,
                "Aux1" => Keys.AUX_1,
                "Aux2" => Keys.AUX_2,
                "Aux3" => Keys.AUX_3,
                "Aux4" => Keys.AUX_4,
                "Aux5" => Keys.AUX_5,
                "Aux6" => Keys.AUX_6,
                "Aux7" => Keys.AUX_7,
                "Aux8" => Keys.AUX_8,
                "Aux9" => Keys.AUX_9,
                "Aux10" => Keys.AUX_10,
                "Aux11" => Keys.AUX_11,
                "Aux12" => Keys.AUX_12,
                "Aux13" => Keys.AUX_13,
                "Aux14" => Keys.AUX_14,
                "MenuBtn" => Keys.MENU,
                "LeftBtn" => Keys.LEFT,
                "RightBtn" => Keys.RIGHT,
                "LRBtn" => Keys.LRBTN,
                "PlusBtn" => Keys.PLUS,
                "MinusBtn" => Keys.MINUS,
                "Reset" => Keys.RESET,
                _ => 0,
            };
        }
        public bool QueueKey(string key)
        {
            if (_menu_locked && key == "RightBtn")
            {
                SendKey("LRBtn");
                return true;
            }
            else
            {
                SendKey(key);
                Thread.Sleep(200);
                return false;
            }
        }

        private void SendKey(string key)
        {
            try
            {
                if (_netStream != null)
                {
                    List<byte> queData = new();

                    Keys bKey = GetKey(key);

                    queData.Add(_FRAME_DLE);
                    queData.Add(_FRAME_STX);
                    queData.Add(0x00);

                    queData.Add(_WIRED_LOCAL_KEY_EVENT);
                    byte[] aBytes = BitConverter.GetBytes((int)bKey);
                    queData.AddRange(aBytes.ToList());
                    queData.AddRange(aBytes.ToList());

                    short crc = 0;
                    foreach (byte aB in queData) { crc += aB; }
                    queData.AddRange(BitConverter.GetBytes(crc).Reverse().ToArray());

                    for (int i = queData.Count - 1; i > 1; i--)
                    {
                        if (queData[i] == 0x10) { queData.Insert(i + 1, 0x00); }
                    }

                    queData.Add(_FRAME_DLE);
                    queData.Add(_FRAME_ETX);

                    System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}", key, BitConverter.ToString(queData.ToArray())));

                    // Send key

                    _netStream.Write(queData.ToArray(), 0, queData.Count);
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }

        }
        public SocketData Update()
        {
            byte[] kaBytes = new byte[] { 0x10, 0x02, 0x01, 0x01, 0x00, 0x14, 0x10, 0x03 };
            byte[] frBytes = new byte[] { 0x00, 0xe0, 0x00, 0xe6, 0x18, 0x1e, 0xe0 };
            SocketData socketData = new();

            try
            {
                int loop = 0;
                while (_tcpClient.Available > 6)
                {
                    loop++;
                    List<byte> recData = new();

                    byte pByte = 0;
                    byte aByte = 0;
                    recData.Clear();

                    // read segment

                    _lTick = _cTick;
                    _cTick = DateTime.Now.Ticks;
                    while (_tcpClient.Available > 0)
                    {
                        pByte = aByte;
                        aByte = (byte)_netStream.ReadByte();

                        if (aByte != 0x00 || pByte != 0x10)
                        {
                            recData.Add(aByte);
                        }

                        if ((aByte == 0x03 && pByte == 0x10) || (aByte == 0xE0 && pByte == 0x1E))
                        {
                            break;
                        }
                    }
                    byte[] bytes = recData.ToArray();

                    //System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}  {2}", (_cTick - _lTick) / 10000, loop, BitConverter.ToString(bytes)));

                    // process segment

                    if (bytes.SequenceEqual(frBytes))
                    {
                        //System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}  {2}", (_cTick - _lTick) / 10000, loop, BitConverter.ToString(bytes)));
                    }
                    else if (bytes.SequenceEqual(kaBytes))
                    {
                        //System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}  {2}", (_cTick - _lTick) / 10000, loop, BitConverter.ToString(bytes)));
                    }
                    else if (bytes.Length > 6)
                    {
                        //System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}  {2}", (_cTick - _lTick) / 10000, loop, BitConverter.ToString(bytes)));

                        // Calculate CRC

                        short crc = 0;
                        for (int i = 0; i < bytes.Length - 4; i++)
                        {
                            crc += bytes[i];
                        }
                        int sCRC = (bytes[^4] * 256) + bytes[^3];

                        if (sCRC == crc)
                        {
                            if (bytes[2] == 0x01 && bytes[3] == 0x02) // LEDs
                            {
                                socketData.Status = (States)BitConverter.ToInt32(bytes, 4);
                                socketData.Blink = (States)BitConverter.ToInt32(bytes, 8);
                                socketData.Valid = true;
                            }
                            else if (bytes[2] == 0x01 && bytes[3] == 0x03) // Display
                            {
                                socketData.DisplayText = Byte2string(bytes, 4, bytes.Length - 9);
                                socketData.Valid = true;
                                if (socketData.DisplayText.Contains("Air Temp"))
                                {
                                    _airT = GetTemp(socketData.DisplayText);
                                    socketData.LogText = DateTime.Now.ToString() + "," + _airT.ToString() + "," + _poolT.ToString(); // Update only after air T
                                }
                                else if (socketData.DisplayText.Contains("Pool Temp"))
                                {
                                    _poolT = GetTemp(socketData.DisplayText);
                                }
                                _menu_locked = socketData.DisplayText.Contains("Menu-Locked");
                            }
                            else if (bytes[2] == 0x00 && bytes[3] == 0x02)
                            {
                                //System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}  {2}", (_cTick - _lTick) / 10000, loop, BitConverter.ToString(bytes)));
                            }
                            else
                            {
                                //System.Diagnostics.Debug.WriteLine(string.Format("{0,10}    {1}  {2}", (_cTick - _lTick) / 10000, loop, BitConverter.ToString(bytes)));
                            }
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine(string.Format("{0}   {1}", "CRC Error", BitConverter.ToString(bytes)));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                System.Diagnostics.Debug.WriteLine(e.Message);
            }
            return socketData;
        }

        private static int GetTemp(string str)
        {
            if (str.Contains("Temp"))
            {
                _ = Int32.TryParse(str.AsSpan(str.Length - 4, 2), out int num);
                return num;
            }
            return 0;
        }
        private static string Byte2string(byte[] bytes, int istr, int slen)
        {
            string tStr = "";
            string bStr = "";
            int isplt = istr + slen / 2;
            for (int i = istr; i < istr + slen - 1; i++)
            {
                if (bytes[i] == 0) { break; }
                string cc = bytes[i] < 128 ? Convert.ToChar(bytes[i]).ToString() : Convert.ToChar(bytes[i] - 128).ToString();

                if (i < isplt)
                {
                    tStr += cc.ToString();
                }
                else
                {
                    if (bytes[i] != 186 && bytes[i - 1] != 186)
                    {
                        if (bytes[i] > 127 && bytes[i - 1] < 128)
                        {
                            bStr += "[";
                        }
                        else if (bytes[i] < 128 && bytes[i - 1] > 127)
                        {
                            bStr += "]";
                        }
                    }
                    bStr += cc.ToString();
                }
            }
            if (bStr.Contains('[') && !bStr.Contains(']')) { bStr += "]"; }
            string str = tStr.Trim() + "\n" + bStr.Trim();
            return str.Replace("_", "°").Trim();
        }

        public static long PingUART(string ipAddr)
        {
            Ping pingSender = new();
            IPAddress ipAddress = IPAddress.Parse(ipAddr);
            PingReply reply = pingSender.Send(ipAddress);
            if (reply.Status == IPStatus.Success)
            {
                //System.Diagnostics.Debug.WriteLine("Address: {0}", reply.Address.ToString());
                //System.Diagnostics.Debug.WriteLine("RoundTrip time: {0}", reply.RoundtripTime);
                //System.Diagnostics.Debug.WriteLine("Time to live: {0}", reply.Options.Ttl);
                //System.Diagnostics.Debug.WriteLine("Don't fragment: {0}", reply.Options.DontFragment);
                //System.Diagnostics.Debug.WriteLine("Buffer size: {0}", reply.Buffer.Length);
                return reply.RoundtripTime;
            }
            else
            {
                return -1;
            }
        }

        public static void WriteTextFile(string fPath, string line)
        {
            if (!File.Exists(fPath))
            {
                File.WriteAllText(fPath, "Time,Air T,Water T\n");
            }
            using StreamWriter file = new(fPath, append: true);
            file.WriteLine(line);
        }
    }
}
