# This should be executable in either bash or PowerShell

openssl req -x509 -nodes -new -sha256 -days 1024 -newkey rsa:4096 -keyout selfSigned.key -out selfSigned.pem -subj "/C=GB/ST=Somewhere/L=Somewhere/O=Somebody/CN=SelfSigned"
openssl pkcs12 -in selfSigned.pem -inkey selfSigned.key -export -out selfSigned.pfx -password pass:yourPasswordHere
rm selfSigned.pem
rm selfSigned.key