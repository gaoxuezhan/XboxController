using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace XInputReporter
{
    /// <summary>
    /// 自定义回调事件参数
    /// </summary>
    /// <typeparam name="T">泛型类返回</typeparam>
    public class TEventArgs<T> : EventArgs
    {
        public T Result { get; private set; }
        public TEventArgs(T obj)
        {
            this.Result = obj;
        }
    }

    class MyTcpClient
    {
        private string md5id;
        Thread readThread;
        Thread heartbeatThread;
        TcpClient tcpClient;
        NetworkStream ns;
        //AsyncOperation会在创建他的上下文执行回调
        public AsyncOperation AsyncOperation;
        private static MyTcpClient singleton = null;
        static readonly object lazylock = new object();

        #region Event
        //回调代理中处理事件
        public event EventHandler Connected;
        public event EventHandler<TEventArgs<Exception>> Error;

        //AsyncOperation回调代理
        private SendOrPostCallback OnConnectedDelegate;
        private SendOrPostCallback OnReceiveDelegate;
        private SendOrPostCallback OnErrorDelegate;

        private void OnConnected(object obj)
        {
            Connected?.Invoke(this, EventArgs.Empty);
        }


        private void OnError(object obj)
        {
            Error?.Invoke(this, new TEventArgs<Exception>((Exception)obj));
        }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        MyTcpClient()
        {
            OnConnectedDelegate = new SendOrPostCallback(OnConnected);
            OnErrorDelegate = new SendOrPostCallback(OnError);
        }

        /// <summary>
        /// 单例模式
        /// </summary>
        /// <returns></returns>
        public static MyTcpClient getInstance()
        {
            if (singleton == null)
            {
                lock (lazylock)
                {
                    if (singleton == null)
                    {
                        singleton = new MyTcpClient();
                    }
                }
            }
            return singleton;
        }

        //当前客户端唯一id
        public string Md5id
        {
            get
            {
                return md5id;
            }

            set
            {
                md5id = value;
            }
        }

        /// <summary>
        /// 连接服务器
        /// </summary>
        public void Connect()
        {

            try
            {
                tcpClient = new TcpClient("192.168.1.24", 23);
                if (tcpClient.Connected)
                {
                    ns = tcpClient.GetStream();
                    //开启两个线程长连接，一个读取，一个心跳
                    readThread = new Thread(Read);
                    readThread.IsBackground = true;
                    readThread.Start();
                    heartbeatThread = new Thread(HeartBeat);
                    heartbeatThread.IsBackground = true;
                    heartbeatThread.Start();
                    System.Diagnostics.Debug.WriteLine("服务器连接成功");
                }
            }
            catch (Exception e)
            {
                this.AsyncOperation.Post(OnErrorDelegate, e);
                Thread.Sleep(5000);
                ReConnect();
            }
        }
        /// <summary>
        /// 读取接收到的数据
        /// </summary>
        private void Read()
        {
            try
            {
                //休眠2秒让窗口初始化
                Thread.Sleep(2000);
                Byte[] readBuffer = new Byte[1024];
                while (true)
                {
                    int alen = tcpClient.Available;
                    if (alen > 0)
                    {
                        Int32 bytes = ns.Read(readBuffer, 0, alen);

                        string responseData = System.Text.Encoding.UTF8.GetString(readBuffer, 0, bytes);
                        //为了避免粘包现象，以\r\n作为分割符
                        string[] arr = responseData.Split("\r\n".ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                        foreach (var item in arr)
                        {
                            if (item != string.Empty)
                            {
                                System.Diagnostics.Debug.WriteLine("接受到消息" + item);
                            }
                        }
                    }
                    Thread.Sleep(500);
                }
            }
            catch (Exception e)
            {
                this.AsyncOperation.Post(OnErrorDelegate, e);
            }
        }

        /// <summary>
        /// 心跳线程
        /// </summary>
        private void HeartBeat()
        {
            try
            {
                while (true)
                {
                    Thread.Sleep(8000);
                    byte[] wb = System.Text.Encoding.UTF8.GetBytes("+h");
                    ns.Write(wb, 0, wb.Length);
                }
            }
            catch (Exception e)
            {
                this.AsyncOperation.Post(OnErrorDelegate, e);
                Thread.Sleep(5000);
                ReConnect();
            }
        }

        /// <summary>
        /// 心跳失败，则网络异常，重新连接
        /// </summary>
        public void ReConnect()
        {
            if (readThread != null)
            {
                readThread.Abort();
            }

            Connect();
        }


        public void SendMsg(string msg)
        {
            byte[] wb = System.Text.Encoding.UTF8.GetBytes(msg);
            ns.Write(wb, 0, wb.Length);
        }
    }
}
