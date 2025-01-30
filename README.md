# Sokgo – Linux SOCKS5 server

### Supported features
- '*Tcp Connect*' method
- '*Udp Associate*' method, with full peer-to-peer compatibility
- DNS resolution
- IPv4/IPv6
- No authentication


### Install for Ubuntu
#### Supported .NET environments
- Dotnet SDK package
- Dotnet SDK snap
- Mono Devel

#### Required packages
1. with Dotnet SDK package :
    - build-essential
    - automake
    - pkgconf
    - dotnet-sdk-8.0

          sudo apt install build-essential automake pkgconf dotnet-sdk-8.0
2. with Dotnet SDK snap :
    - build-essential
    - automake
    - pkgconf
    - snap : dotnet-sdk (classic)

          sudo apt install build-essential automake pkgconf
          sudo snap install dotnet-sdk --classic

3. with Mono Devel :
    - build-essential
    - automake
    - pkgconf
    - libtool
    - mono-devel

          sudo apt install build-essential automake pkgconf libtool mono-devel

#### Generate makefiles
1. with Dotnet SDK :

       ./prj/ac-dotnet/autogen.sh [--enable-debug]
2. with Mono Devel :

       ./prj/ac-mono/autogen.sh [--enable-debug]

#### Build binaries
    make
#### Install binaries (need privilege)
    sudo make install

#### Edit config file (need privilege)
    sudo nano /usr/local/etc/sokgo.config
See the *Config* section below for the description of the settings.

#### Start daemon (need privilege)
- systemd service :

      sudo systemctl start sokgo.service
- System-V :

      sudo /etc/init.d/sokgo start


### Config
Edit the xml config file `/usr/local/etc/sokgo.config` :
- **ListenPort** (default: 1080)  
The Tcp port of the server.  

- **ListenHost** (default: "")  
The hostname of the server to listen on, can be an IPv4 address of an interface.  
Empty to listen on all interfaces (equivalent to "0.0.0.0").  
Ex: "localhost", "192.168.1.10"  

- **ListenHostIPv6** (default: "", disabled)  
Empty to disable IPv6. Use "::" to listen on all IPv6 interfaces.  
Ex: "::", "2800::1"  

- **ListenUdpPortRangeMin** (default: 32768),  
**ListenUdpPortRangeMax** (default: 65535)  
In the result of '*Udp Associate*', this is the port range used to create the Udp socket to communicate with the client.  

- **PublicHost** (default: "")  
The hostname to resolve for IPv4 and to return to the client in the '*Udp Associate*' response.  
Empty (default) to use the destination IP of the socket in the '*Udp Associate*' requested from the client to the server.  
This can be useful if the server is behind a NAT and you know the external hostname for the server. Can be an IP or a DNS name to resolve. 

- **PublicHostIPv6** (default: "")  
The hostname to resolve for IPv6 and to return to the client in the '*Udp Associate*' response.  
See PublicHost (IPv4) above.  

- **OutgoingUdpPortRangeMin** (default: 32768),  
**OutgoingUdpPortRangeMax** (default: 65535)  
For the '*Udp Associate*' protocol, this is the port range used to create the sockets to communicate with the destination peers (for the client requesting the connection, see ListenUdpPortRangeMin and ListenUdpPortRangeMax).  

- **SelectThreadCount** (default: 8)  
Number of worker threads dedicated to handle active sockets.  

- **SelectSocketMax** (default: 200)  
Max number of sockets managed by a worker thread.  
