# HTTPS Certificates

Put any HTTPS certificate files here and amend `appsettings.yml` to point to them.

Unless you modify the `appsettings.yml` files to remove the `HttpServer:Endpoints:Https` section, you **will**
need to supply a certificate.

There is a self-signed PKCS#12 certificate supplied (created by the [`createSelfSigned.ps1` script](./createSelfSigned.ps1)) but
this is **no good** for actual production use, unless you are only planning to use this proxy via HTTP. Visual Studio
will **not** send Basic Authentication credentials to an untrusted server.
