using System.Text;

var net = new Network();

var h1 = net.Open(1024);
h1.CombineFinAck = true;
h1.Listen(19);

var h2 = net.Open(1903);
h2.Sniff = true;
h2.Connect(1024, 34);

h2.Send(Encoding.UTF8.GetBytes("GET / HTTP/1.1\r\n"));
h1.Send(Encoding.UTF8.GetBytes("Hello, world!\r\n"));

h2.Close();