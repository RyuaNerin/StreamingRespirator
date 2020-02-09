@echo off

echo gen ca.key
openssl genrsa      ^
		-aes256     ^
		-out ca.key ^
		2048


echo gen ca.csr
openssl req            ^
        -new           ^
        -sha256        ^
        -key    ca.key ^
        -out    ca.csr ^
        -config ca.conf

echo gen ca.crt
openssl x509               ^
        -req               ^
        -days       3650   ^
        -extensions v3_ca  ^
        -CAcreateserial    ^
        -in         ca.csr ^
        -signkey    ca.key ^
        -out        ca.crt ^
        -extfile    ca.conf

REM ##################################################

echo gen client.key
openssl genrsa                  ^
		-aes256                 ^
		-out     client.key.enc ^
        -passout pass:12345678  ^
		2048

openssl rsa                   ^
		-in  client.key.enc   ^
		-passin pass:12345678 ^
		-out client.key

del client.key.enc

echo gen client.csr
openssl req                ^
        -new               ^
        -sha256            ^
        -key    client.key ^
        -out    client.csr ^
        -config client.conf

echo gen client.crt
openssl x509                   ^
        -req                   ^
        -days       3650       ^
        -extensions v3_user    ^
        -CAcreateserial        ^
        -CA         ca.crt     ^
        -CAkey      ca.key     ^
        -in         client.csr ^
        -out        client.crt ^
        -extfile    client.conf
        
echo gen client.p12
openssl pkcs12               ^
        -export              ^
        -certfile ca.crt     ^
        -in       client.crt ^
        -inkey    client.key ^
        -out      client.p12 ^
        -passout  pass: