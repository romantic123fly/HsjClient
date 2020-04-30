using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetManager : Singleton<NetManager>
{
    private string serverIP;
    private int serverPort;
    public string PublicKey = "幻世界";//公钥前后端一致
    public string SecretKey { get; set; }//等待后端传输赋值
    private Socket m_Socket;//客户端Socket
    private ByteArray m_ReadBuff;

    private Thread m_MsgThread;
    private Thread m_HeartThread;

    static long lastPingTime;
    static long lastPongTime;

    private Queue<ByteArray> m_WriteQueue;

    private List<MsgBase> m_MsgList;
    private List<MsgBase> m_UnityMsgList;

    public static long m_PingInterval = 5;//心跳间隔

    public delegate void EventListener(string str);
    public delegate void ProtoListener(MsgBase msg);
    private Dictionary<ProtocolEnum, ProtoListener> m_ProtoDic = new Dictionary<ProtocolEnum, ProtoListener>();

    private bool m_IsConnectSuccessed = false; //是否链接成功过（只要链接成功过就是true，再也不会变成false）
    private bool m_ReConnect = false;

    private NetworkReachability m_CurNetWork = NetworkReachability.NotReachable;
    /// <summary>
    /// 切换网络类型，链接重置
    /// </summary>
    /// <returns></returns>
    public IEnumerator CheckNetType()
    {
        m_CurNetWork = Application.internetReachability;
        while (true)
        {
            yield return new WaitForSeconds(1);
            if (m_IsConnectSuccessed)
            {
                if (m_CurNetWork != Application.internetReachability)
                {
                    ReConnect();
                    m_CurNetWork = Application.internetReachability;
                }
            }
        }
    }


    /// <summary>
    /// 一个协议希望只有一个监听
    /// </summary>
    /// <param name="protocolEnum"></param>
    /// <param name="listener"></param>
    public void AddProtoListener(ProtocolEnum protocolEnum, ProtoListener listener)
    {
        m_ProtoDic[protocolEnum] = listener;
    }

    public void FirstProto(ProtocolEnum protocolEnum, MsgBase msgBase)
    {
        Debug.Log(protocolEnum);
        if (m_ProtoDic.ContainsKey(protocolEnum))
        {
            m_ProtoDic[protocolEnum](msgBase);
        }
    }

    public void Update()
    {

        //断开链接后，链接服务器之后自动登录
        if (!string.IsNullOrEmpty(SecretKey) && m_Socket.Connected && m_ReConnect)
        {
            //在本地保存了我们的账户和token，然后进行判断有无账户和token，

            //使用token登录
            //ProtocolMgr.Login(LoginType.Token, "username", "token", (res, restoken) =>
            //{
            //    if (res == LoginResult.Success)
            //    {

            //    }
            //    else
            //    {

            //    }
            //});
            //m_ReConnect = false;
        }

        MsgUpdate();
    }

    void MsgUpdate()
    {
        if (m_Socket != null && m_Socket.Connected)
        {
            MsgBase msgBase = null;
            lock (m_UnityMsgList)
            {
                if (m_UnityMsgList.Count > 0)
                {
                    msgBase = m_UnityMsgList[0];
                    m_UnityMsgList.RemoveAt(0);
                }
            }
            if (msgBase != null)
            {
                FirstProto(msgBase.ProtoType, msgBase);
            }
        }
    }

    void MsgThread()
    {
        while (m_Socket != null && m_Socket.Connected)
        {
            if (m_MsgList.Count <= 0) continue;

            MsgBase msgBase = null;
            lock (m_MsgList)
            {
                if (m_MsgList.Count > 0)
                {
                    msgBase = m_MsgList[0];
                    m_MsgList.RemoveAt(0);
                }
            }

            if (msgBase != null)
            {
                if (msgBase is MsgPing)
                {
                    lastPongTime = GetTimeStamp();
                    Debug.Log("收到心跳包！！！！！！！");
                }
                else
                {
                    lock (m_UnityMsgList)
                    {
                        m_UnityMsgList.Add(msgBase);
                    }
                }
            }
            else
            {
                break;
            }
        }
    }

    void PingThread()
    {
        while (m_Socket != null && m_Socket.Connected)
        {
            long timeNow = GetTimeStamp();
            if (timeNow - lastPingTime > m_PingInterval)
            {
                MsgPing msgPing = new MsgPing();
                SendMessage(msgPing);
                lastPingTime = GetTimeStamp();
            }

            //如果心跳包过长时间没收到，就关闭连接
            if (timeNow - lastPongTime > m_PingInterval * 4)
            {
                ClientClose();
            }
        }
    }

    /// <summary>
    /// 重连方法
    /// </summary>
    public void ReConnect()
    {
        Connect(serverIP, serverPort);
        m_ReConnect = true;
    }

    /// <summary>
    /// 链接服务器函数
    /// </summary>
    /// <param name="ip"></param>
    /// <param name="port"></param>
    public void Connect(string ip, int port)
    {
        if (m_Socket != null && m_Socket.Connected)
        {
            Debug.LogError("链接失败，已经链接了！");
            return;
        }
        serverIP = ip;
        serverPort = port;
        lastPingTime = GetTimeStamp();
        lastPongTime = GetTimeStamp();

        //初始化变量
        m_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        m_ReadBuff = new ByteArray();
        m_WriteQueue = new Queue<ByteArray>();
        m_MsgList = new List<MsgBase>();
        m_UnityMsgList = new List<MsgBase>();

        m_Socket.NoDelay = true;
        m_Socket.BeginConnect(serverIP, serverPort, ConnectCallback, m_Socket);
    }

    /// <summary>
    /// 链接回调
    /// </summary>
    /// <param name="ar"></param>
    void ConnectCallback(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            socket.EndConnect(ar);

            m_IsConnectSuccessed = true;
            m_MsgThread = new Thread(MsgThread);
            m_MsgThread.IsBackground = true;
            m_MsgThread.Start();

            m_HeartThread = new Thread(PingThread);
            m_HeartThread.IsBackground = true;
            m_HeartThread.Start();
            ProtocolMgr.SecretRequest();
            Debug.Log("Socket Connect Success");
            m_Socket.BeginReceive(m_ReadBuff.Bytes, m_ReadBuff.WriteIdx, m_ReadBuff.Remain, 0, ReceiveCallBack, socket);
        }
        catch (SocketException ex)
        {
            Debug.LogError("Socket Connect fail:" + ex.ToString());
        }
    }


    /// <summary>
    /// 接受数据回调
    /// </summary>
    /// <param name="ar"></param>
    void ReceiveCallBack(IAsyncResult ar)
    {
        try
        {
            Socket socket = (Socket)ar.AsyncState;
            int count = socket.EndReceive(ar);
            if (count <= 0) { ClientClose(); return; }
            m_ReadBuff.WriteIdx += count;
            OnReceiveData();
            if (m_ReadBuff.Remain < 8)
            {
                m_ReadBuff.MoveBytes();
                m_ReadBuff.ReSize(m_ReadBuff.Length * 2);
            }
            socket.BeginReceive(m_ReadBuff.Bytes, m_ReadBuff.WriteIdx, m_ReadBuff.Remain, 0, ReceiveCallBack, socket);
        }
        catch (SocketException ex)
        {
            Debug.LogError("Socket ReceiveCallBack fail:" + ex.ToString());
            ClientClose();

        }
    }

    /// <summary>
    /// 对数据进行处理
    /// </summary>
    void OnReceiveData()
    {
        if (m_ReadBuff.Length <= 4 || m_ReadBuff.ReadIdx < 0)
            return;

        int readIdx = m_ReadBuff.ReadIdx;
        byte[] bytes = m_ReadBuff.Bytes;
        int bodyLength = BitConverter.ToInt32(bytes, readIdx);
        //读取协议长度之后进行判断，如果消息长度小于读出来的消息长度，证明是没有一条完整的数据
        if (m_ReadBuff.Length < bodyLength + 4)
        {
            return;
        }

        m_ReadBuff.ReadIdx += 4;
        int nameCount = 0;
        ProtocolEnum protocol = MsgBase.DecodeName(m_ReadBuff.Bytes, m_ReadBuff.ReadIdx, out nameCount);
        if (protocol == ProtocolEnum.None)
        {
            Debug.LogError("OnReceiveData MsgBase.DecodeName fail");
            ClientClose();
            return;
        }

        m_ReadBuff.ReadIdx += nameCount;
        //解析协议体
        int bodyCount = bodyLength - nameCount;
        try
        {
            MsgBase msgBase = MsgBase.Decode(protocol, m_ReadBuff.Bytes, m_ReadBuff.ReadIdx, bodyCount);
            if (msgBase == null)
            {
                Debug.LogError("接受数据协议内容解析出错");
                ClientClose();
                return;
            }
            m_ReadBuff.ReadIdx += bodyCount;
            m_ReadBuff.CheckAndMoveBytes();
            //协议具体的操作
            lock (m_MsgList)
            {
                m_MsgList.Add(msgBase);
            }
            //处理粘包
            if (m_ReadBuff.Length > 4)
            {
                OnReceiveData();
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("Socket OnReceiveData error:" + ex.ToString());
            ClientClose();
        }
    }

    /// <summary>
    /// 发送数据到服务器
    /// </summary>
    /// <param name="msgBase"></param>
    public void SendMessage(MsgBase msgBase)
    {
        if (m_Socket == null || !m_Socket.Connected) return;
        try
        {
            byte[] nameBytes = MsgBase.EncodeName(msgBase);
            byte[] bodyBytes = MsgBase.Encond(msgBase);
            int len = nameBytes.Length + bodyBytes.Length;
            byte[] byteHead = BitConverter.GetBytes(len);
            byte[] sendBytes = new byte[byteHead.Length + len];
            Array.Copy(byteHead, 0, sendBytes, 0, byteHead.Length);
            Array.Copy(nameBytes, 0, sendBytes, byteHead.Length, nameBytes.Length);
            Array.Copy(bodyBytes, 0, sendBytes, byteHead.Length + nameBytes.Length, bodyBytes.Length);
            ByteArray ba = new ByteArray(sendBytes);
            lock (m_WriteQueue)
            {
                m_WriteQueue.Enqueue(ba);
                if (m_WriteQueue.Count == 1)
                {
                    m_Socket.BeginSend(sendBytes, 0, sendBytes.Length, 0, SendCallBack, m_Socket);
                }
                else
                {
                    Debug.LogError("消息队列数量不止一个");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("SendMessage error:" + ex.ToString());
            ClientClose();
        }
    }

    /// <summary>
    /// 发送结束回调
    /// </summary>
    /// <param name="ar"></param>
    void SendCallBack(IAsyncResult ar)
    {
        Socket socket = (Socket)ar.AsyncState;
        if (socket == null || !socket.Connected) return;
        int count = socket.EndSend(ar);
        //判断是否发送完成
        ByteArray ba;
        lock (m_WriteQueue)
        {
            ba = m_WriteQueue.First();
        }
        ba.ReadIdx += count;
        //代表发送完整
        if (ba.Length == 0)
        {
            lock (m_WriteQueue)
            {
                m_WriteQueue.Dequeue();
                if (m_WriteQueue.Count > 0)//发送完整且存在第二条数据
                {
                    ba = m_WriteQueue.First();
                    socket.BeginSend(ba.Bytes, ba.ReadIdx, ba.Length, 0, SendCallBack, socket);
                }
                else
                {
                    ba = null;
                }
            }
        }
        else//发送不完整
        {
            socket.BeginSend(ba.Bytes, ba.ReadIdx, ba.Length, 0, SendCallBack, socket);
        }
    }

    /// <summary>
    /// 关闭链接
    /// </summary>
    /// <param name="normal"></param>
    public void ClientClose()
    {
        SecretKey = "";
        m_Socket.Close();
        if (m_HeartThread != null && m_HeartThread.IsAlive)
        {
            m_HeartThread.Abort();
            m_HeartThread = null;
        }
        if (m_MsgThread != null && m_MsgThread.IsAlive)
        {
            m_MsgThread.Abort();
            m_MsgThread = null;
        }
        Debug.Log("Close Socket");
        if (m_IsConnectSuccessed)
        {
#if UNITY_EDITOR

#else
             ReConnect();
#endif
        }
    }
    public static long GetTimeStamp()
    {
        TimeSpan ts = DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0);
        return Convert.ToInt64(ts.TotalSeconds);
    }
}
