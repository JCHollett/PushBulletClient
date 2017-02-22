using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PushBulletClient {

    public class Beat {
        public string type { get; set; }
        public string subtype { get; set; }
        public PBData push { get; set; }

        public override string ToString() {
            return JsonConvert.SerializeObject( this );
        }
    }

    public class WebSocketListener {
        private readonly object locker = new object();
        private ClientWebSocket Stream;
        private AccessToken Token;
        private static string BaseUrl = "wss://stream.pushbullet.com/websocket/";
        private Uri Address { get; set; }
        public ConcurrentQueue<Beat> Queue { get; private set; }
        private bool Running { get; set; }

        public WebSocketListener( AccessToken auth ) {
            this.Token = auth;
            this.Stream = new ClientWebSocket();
            this.Address = new Uri( BaseUrl + auth );
            this.Queue = new ConcurrentQueue<Beat>();
        }

        public async Task<bool> Connect() {
            await this.Stream.ConnectAsync( this.Address , CancellationToken.None );
            while( this.Stream.State == WebSocketState.Connecting ) {
                Thread.Sleep( 200 );
            }
            if( Stream.State == WebSocketState.Open ) {
                return true;
            }
            return false;
        }

        public async Task<string> Read() {
            ArraySegment<byte> b = new ArraySegment<byte>(new byte[8192]);
            WebSocketReceiveResult received = null;
            using( var mem = new MemoryStream() ) {
                do {
                    received = await this.Stream.ReceiveAsync( b , CancellationToken.None );
                    mem.Write( b.Array , b.Offset , received.Count );
                } while( !received.EndOfMessage );
                mem.Seek( 0 , SeekOrigin.Begin );
                using( var read = new StreamReader( mem , Encoding.UTF8 ) )
                    return read.ReadToEnd();
            }
        }

        public Beat Next() {
            Beat b = null;
            this.Queue.TryDequeue(out b);
            return b;
        }

        public async Task<bool> Listen() {
            this.Running = true;
            do {
                var j = JsonConvert.DeserializeObject<Beat>( await this.Read() );
                if( !j.type.Equals( "nop" ) )
                    this.Queue.Enqueue( j );
            } while( this.Running );
            return true;
        }

        public void Stop() {
            this.Running = false;
            this.Stream.CloseAsync( WebSocketCloseStatus.Empty , "Server Shutting Down" , CancellationToken.None );
        }
    }
}