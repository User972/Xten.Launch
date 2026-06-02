#!/usr/bin/env bash
# Provision an Azure VM that runs the nop-ebook docker-compose stack (lift-and-shift).
#
# This does NOT change the app: the VM just runs deploy/docker-compose.yml as-is
# (nopcommerce + postgres + redis + caddy + db-backup). Azure only provides the box,
# a public IP/FQDN, and the firewall. TLS, DB, cache, and backups stay inside compose.
#
# Prereqs on your machine: Azure CLI (`az login` done), bash.
# Run from anywhere:  bash deploy/azure/provision-vm.sh
#
# Override any default inline, e.g.:  RG=my-rg LOCATION=southeastasia bash deploy/azure/provision-vm.sh
set -euo pipefail

# ---- tunables ----
RG=${RG:-nop-ebook-rg}
LOCATION=${LOCATION:-southeastasia}          # close to an Indonesian audience
VM_NAME=${VM_NAME:-nop-ebook-vm}
VM_SIZE=${VM_SIZE:-Standard_B2ms}            # 2 vCPU / 8 GiB. The compose Postgres tuning assumes a ~4 GB
                                             # box; 8 GiB leaves headroom for app+pg+redis + the in-VM build.
ADMIN_USER=${ADMIN_USER:-azureuser}
DNS_LABEL=${DNS_LABEL:-nop-ebook-$RANDOM}    # -> <label>.<region>.cloudapp.azure.com (usable as DOMAIN)
OS_DISK_GB=${OS_DISK_GB:-64}                 # room for the .NET SDK image + nopCommerce clone + volumes
# Lock SSH to the IP you're running this from. Set SSH_SOURCE='*' to allow any (not recommended).
SSH_SOURCE=${SSH_SOURCE:-"$(curl -fsS https://api.ipify.org)/32"}

echo ">> Resource group $RG in $LOCATION"
az group create -n "$RG" -l "$LOCATION" -o none

# ---- cloud-init: install Docker Engine + Compose v2 and let the admin user run docker ----
CLOUD_INIT=$(mktemp)
cat > "$CLOUD_INIT" <<EOF
#cloud-config
package_update: true
packages: [ca-certificates, curl, git]
runcmd:
  - install -m 0755 -d /etc/apt/keyrings
  - curl -fsSL https://download.docker.com/linux/ubuntu/gpg -o /etc/apt/keyrings/docker.asc
  - chmod a+r /etc/apt/keyrings/docker.asc
  - echo "deb [arch=\$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.asc] https://download.docker.com/linux/ubuntu \$(. /etc/os-release && echo \$VERSION_CODENAME) stable" > /etc/apt/sources.list.d/docker.list
  - apt-get update
  - DEBIAN_FRONTEND=noninteractive apt-get install -y docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  - usermod -aG docker ${ADMIN_USER}
  - systemctl enable --now docker
EOF

echo ">> Creating VM $VM_NAME ($VM_SIZE, Ubuntu 24.04)"
az vm create \
  -g "$RG" -n "$VM_NAME" \
  --image Ubuntu2404 \
  --size "$VM_SIZE" \
  --admin-username "$ADMIN_USER" \
  --generate-ssh-keys \
  --public-ip-sku Standard \
  --public-ip-address-dns-name "$DNS_LABEL" \
  --os-disk-size-gb "$OS_DISK_GB" \
  --custom-data "$CLOUD_INIT" \
  -o none
rm -f "$CLOUD_INIT"

# ---- firewall (NSG) ----
NSG=$(az network nsg list -g "$RG" --query "[0].name" -o tsv)
echo ">> Opening ports on NSG $NSG (80, 443/tcp, 443/udp for HTTP/3; SSH limited to $SSH_SOURCE)"
az network nsg rule create -g "$RG" --nsg-name "$NSG" -n Allow-HTTP   --priority 1001 \
  --access Allow --protocol Tcp --direction Inbound --destination-port-ranges 80  -o none
az network nsg rule create -g "$RG" --nsg-name "$NSG" -n Allow-HTTPS  --priority 1002 \
  --access Allow --protocol Tcp --direction Inbound --destination-port-ranges 443 -o none
az network nsg rule create -g "$RG" --nsg-name "$NSG" -n Allow-HTTP3  --priority 1003 \
  --access Allow --protocol Udp --direction Inbound --destination-port-ranges 443 -o none
az network nsg rule create -g "$RG" --nsg-name "$NSG" -n Restrict-SSH --priority 1000 \
  --access Allow --protocol Tcp --direction Inbound --destination-port-ranges 22 \
  --source-address-prefixes "$SSH_SOURCE" -o none

FQDN=$(az vm show -d -g "$RG" -n "$VM_NAME" --query fqdns -o tsv)
IP=$(az vm show -d -g "$RG" -n "$VM_NAME" --query publicIps -o tsv)

cat <<DONE

============================================================
VM ready.
  FQDN : $FQDN
  IP   : $IP
  SSH  : ssh ${ADMIN_USER}@${FQDN}

Next (see deploy/azure/README.md for detail):
  1. DNS: either use $FQDN directly as your DOMAIN, or point your
     own domain's A record at $IP (Caddy needs the name to resolve here for TLS).
  2. ssh ${ADMIN_USER}@${FQDN}
  3. git clone <this-repo> && cd <repo>/deploy
  4. cp .env.example .env   # then edit: DOMAIN, ACME_EMAIL, POSTGRES_PASSWORD, REDIS_PASSWORD
  5. docker compose up -d --build      # builds nopCommerce from source on the VM (slow first time)
  6. Browse https://<DOMAIN> -> run the nopCommerce install wizard once (PostgreSQL).
============================================================
DONE
