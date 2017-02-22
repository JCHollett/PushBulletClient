using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace PushBulletClient {

    public class Pushes {
        [JsonProperty( "pushes" )]
        public List<PBData> Data { get; set; }
    }

    public class PBData {
        public bool active { get; set; }
        public string iden { get; set; }
        public float created { get; set; }
        public float modified { get; set; }
        public string type { get; set; }
        public bool dismissed { get; set; }
        public string direction { get; set; }
        public string sender_iden { get; set; }
        public string sender_email { get; set; }
        public string sender_email_normalized { get; set; }
        public string sender_name { get; set; }
        public string receiver_iden { get; set; }
        public string receiver_email { get; set; }
        public string receiver_email_normalized { get; set; }
        public string title { get; set; }
        public string body { get; set; }
    }

    public class User {
        public bool active { get; set; }
        public string iden { get; set; }
        public float created { get; set; }
        public float modified { get; set; }
        public string email { get; set; }
        public string email_normalized { get; set; }
        public string name { get; set; }
        public string image_url { get; set; }
        public string max_upload_size { get; set; }
    }

    public struct AccessToken {
        private string Token;

        public AccessToken( string Token ) {
            this.Token = Token;
        }

        public static implicit operator string( AccessToken x ) {
            return x.Token;
        }

        public static implicit operator AccessToken( string x ) {
            return new AccessToken( x );
        }

        public override string ToString() {
            return this.Token;
        }
    }

    public struct Email {
        private string Address;

        public Email( string email ) {
            this.Address = email;
        }

        public static implicit operator string( Email x ) {
            return x.Address;
        }

        public static implicit operator Email( string x ) {
            return new Email( x );
        }

        public override string ToString() {
            return this.Address;
        }
    }

    public struct CellNumber {
        private static long areacode =    5550000000;
        private static long countrycode = 10000000000;
        public static long AreaCode { get { return areacode / 1000000; } set { areacode = value * 1000000; } }
        public static long CountryCode { get { return countrycode / 1000000000; } set { countrycode = value * 1000000000; } }
        private string Number;

        public CellNumber( long Number ) {
            switch( Number.ToString().Length ) {
                case 11:
                    this.Number = Number.ToString( "+# ### ### ####" );
                    break;

                case 10:
                    Number += countrycode;
                    this.Number = Number.ToString( "+# ### ### ####" );
                    break;

                case 7:
                    Number += areacode + countrycode;
                    this.Number = Number.ToString( "+# ### ### ####" );
                    break;

                default:
                    Number = areacode + countrycode + 5555555;
                    this.Number = Number.ToString( "+# ### ### ####" );
                    break;
            }
        }

        public static implicit operator string( CellNumber x ) {
            return x.Number;
        }

        public static implicit operator CellNumber( long x ) {
            return new CellNumber( x );
        }

        public override string ToString() {
            return this.Number;
        }
    }

    public class PBClient {
        private static string BaseUrl = "https://api.pushbullet.com/v2/";
        private static string DEBUG_STRING = null;

        private User user;
        public AccessToken Token { get; set; }
        public Email EmailAddress { get; set; }
        public CellNumber Phone { get; set; }
        private string cellIden { get; set; }
        private string userIden { get { return this.user.iden; } }
        private string userName { get { return this.user.name; } }

        public PBClient( AccessToken token ) {
            this.Token = token;
            this.user = this.GetUser().Result;
            this.EmailAddress = user.email;
        }

        public PBClient( AccessToken token , AccessToken cellIden ) : this( token ) {
        }

        public PBClient( AccessToken token , AccessToken celltoken , CellNumber number ) : this( token ) {
            this.cellIden = cellIden;
            this.Phone = number;
        }

        public async Task<User> GetUser() {
            try {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add( "Access-Token" , this.Token );
                Uri url = new Uri( BaseUrl + "users/me" );
                HttpResponseMessage post = await client.GetAsync( url );
                return JsonConvert.DeserializeObject<User>( await post.Content.ReadAsStringAsync() );
            } catch( Exception e ) {
                Console.WriteLine( e.Data );
            }
            return null;
        }

        public async Task<PBData> PushNote( string Title , string Body , string Email ) {
            try {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add( "Access-Token" , this.Token );
                Uri url = new Uri( BaseUrl + "pushes" );
                Dictionary<string , string> data = new Dictionary<string , string>();
                data.Add( "body" , Body );
                data.Add( "title" , Title );
                data.Add( "email" , Email );
                data.Add( "type" , "note" );
                HttpContent jsonContent = new StringContent( DEBUG_STRING = JsonConvert.SerializeObject( data , new KeyValuePairConverter() ) , System.Text.Encoding.UTF8 , "application/json" );
                Console.WriteLine( DEBUG_STRING );
                HttpResponseMessage post = await client.PostAsync( url , jsonContent );
                return JsonConvert.DeserializeObject<PBData>( await post.Content.ReadAsStringAsync() );
            } catch( Exception e ) {
                Console.WriteLine( e.Data );
            }
            return null;
        }

        public PBData GetNote( Func<PBData , bool> pred ) {
            var pushes = this.GetNotes().Result.ToList();

            var x = pushes.First( p => pred( p ) );
            //var x = pushes.First( y => y != null && y.GetType().GetProperties().ToDictionary(prop=>prop.Name,prop=>prop.GetValue(y).ToString())[pred.Key] == pred.Value.ToString() );
            return x;
        }

        public async Task<IEnumerable<PBData>> GetNotes() {
            try {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add( "Access-Token" , this.Token );
                Uri url = new Uri( BaseUrl + "pushes" );
                HttpResponseMessage post = await client.GetAsync( url );
                var x = JsonConvert.DeserializeObject<Pushes>( DEBUG_STRING = await post.Content.ReadAsStringAsync() );
                return x.Data.Where( y => y.active );
            } catch( Exception e ) {
                Console.WriteLine( e.Data );
            }
            return null;
        }

        public async Task<bool> PushSMS( CellNumber Number , string Message ) {
            try {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add( "Access-Token" , this.Token );
                Uri url = new Uri( BaseUrl + "ephemerals" );
                Dictionary<string , object> data = new Dictionary<string , object>();
                data.Add( "type" , "push" );
                data.Add( "push" , new Dictionary<string , string> {
                    { "conversation_iden", Number},
                    { "message", Message },
                    { "package_name", "com.pushbullet.android" },
                    { "source_user_iden", this.userIden },
                    { "target_device_iden", this.cellIden },
                    { "type", "messaging_extension_reply" }
                } );
                HttpContent jsonContent = new StringContent( DEBUG_STRING = JsonConvert.SerializeObject( data , new KeyValuePairConverter() ) , System.Text.Encoding.UTF8 , "application/json" );
                HttpResponseMessage post = await client.PostAsync( url , jsonContent );
                JsonConvert.DeserializeObject<PBData>( DEBUG_STRING = await post.Content.ReadAsStringAsync() );
                return true;
            } catch( Exception e ) {
                Console.WriteLine( e.Data );
                return false;
            }
        }

        public async Task<bool> DELETE_ALL() {
            try {
                HttpClient client = new HttpClient();
                client.DefaultRequestHeaders.Add( "Access-Token" , key );
                Uri url = new Uri( BaseUrl + "pushes" );
                HttpResponseMessage post = await client.DeleteAsync( url );
                JsonConvert.DeserializeObject<PBData>( DEBUG_STRING = await post.Content.ReadAsStringAsync() );
                return true;
            } catch( Exception e ) {
                Console.WriteLine( e.Data );
                return false;
            }
        }
    }
}