# Deploy to Azure — VM + docker-compose (lift-and-shift)

This provisions a single Azure VM that hosts the **multi-tenant** platform: a shared
`nginx-proxy` + `acme-companion` (auto-TLS) and one isolated stack per tenant
(nopCommerce app + PostgreSQL + nginx). Azure only provides the VM, a public IP/FQDN, and the
firewall; everything else runs in Docker on the box. See [`../README.md`](../README.md) for the
full multi-tenant model — this page only covers standing up the host.

For the managed-PaaS alternative (App Service / Container Apps + Azure Database for PostgreSQL +
Azure Cache for Redis), see the architecture mapping discussed separately — that's more robust
for production but a lot more setup.

## Prerequisites
- **Azure CLI** installed and `az login` completed, on an account that can create resources.
- A **domain** you control *(optional)* — or just use the VM's free
  `…cloudapp.azure.com` FQDN, which Caddy can also get a Let's Encrypt cert for.

## 1. Provision the VM
```bash
bash deploy/azure/provision-vm.sh
```
Override defaults via env vars, e.g.:
```bash
RG=nop-ebook-rg LOCATION=southeastasia VM_SIZE=Standard_B2ms \
DNS_LABEL=my-bookstore bash deploy/azure/provision-vm.sh
```
The script creates: a resource group, an Ubuntu 24.04 VM (Docker + Compose v2 pre-installed via
cloud-init), a public IP with a DNS label, and NSG rules for **80, 443/tcp, 443/udp** (HTTP/3),
with **SSH locked to your current IP**. It prints the FQDN/IP and SSH command when done.

> **Size note:** default `Standard_B2ms` (2 vCPU / 8 GiB). The compose Postgres tuning
> (`shared_buffers=1GB`, …) is sized for a ~4 GB box; 8 GiB gives headroom because the app,
> Postgres, and Redis share the VM **and** nopCommerce is compiled from source on first
> `up --build`. Don't go below 4 GiB.

## 2. DNS
Pick one:
- **Quick start:** use the printed `…cloudapp.azure.com` FQDN as your `DOMAIN`.
- **Custom domain:** create an `A` record (and `AAAA` if you enabled IPv6) pointing at the
  VM's public IP. Caddy issues TLS automatically once the name resolves to the VM.

## 3. Start the shared proxy + add tenants
```bash
ssh azureuser@<FQDN>
git clone <this-repo-url> app && cd app

# Shared reverse proxy (once per VM)
cd deploy/proxy && cp .env.example .env   # set ACME_EMAIL
docker compose up -d && cd ../..

# One tenant per store (builds the shared image on the first run — several minutes)
deploy/scripts/new-customer.sh acme acme.example.co.id ops@acme.co.id
```
> If `docker` asks for sudo, log out/in once so the `docker` group membership applies (cloud-init
> adds your user to it), or prefix with `sudo`.

The first tenant triggers the image build (clones nopCommerce 4.90.4 and compiles it + the Midtrans
plugin); later tenants reuse the image and come up in seconds. Full details and per-tenant
operations are in [`../README.md`](../README.md).

## 4. DNS, TLS, and install
- Point each tenant's domain at this VM's public IP (step 2). `acme-companion` issues TLS once it
  resolves — `docker logs -f nginx-proxy-acme`.
- Browse `https://<tenant-domain>` and run the nopCommerce install wizard once per tenant
  (PostgreSQL, Host `postgres`, user/password from that tenant's `.env`).

## 5. Operate
- **Update all tenants after a code/theme change:** rebuild the shared image then recreate each
  tenant (see [`../README.md`](../README.md) → Operations).
- **Per-tenant logs:** `docker compose -p <slug> logs -f nopcommerce`
- **Backups:** each tenant has a `db-backup` sidecar (nightly `pg_dump -Fc` into its `backups`
  volume). These stay on the VM — push them off-box (e.g. `az storage blob upload-batch` to a
  Storage Account) and run a quarterly restore test (blueprint §11.5).
- **Midtrans:** register each tenant's `https://<domain>/...` webhook URL in its Midtrans dashboard.

## Cost / teardown
A `B2ms` VM + disk + IP is the bulk of the cost. To remove everything:
```bash
az group delete -n nop-ebook-rg --yes --no-wait
```
