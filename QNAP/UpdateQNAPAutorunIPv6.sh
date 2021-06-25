/etc/init.d/init_disk.sh mount_flash_config
cd /tmp/nasconfig_tmp/
touch autorun.sh
chmod +x autorun.sh
echo '#!/bin/sh' >> autorun.sh
echo "/bin/sh -c \"sleep 600 && sed -i 's/VPN_PROTO}$/VPN_PROTO}6/' /etc/init.d/vpn_openvpn.sh && /etc/init.d/vpn_openvpn.sh restart\" &" >> autorun.sh
cd -
/etc/init.d/init_disk.sh umount_flash_config
# Activate autorun.sh settings in ControlPanel > System > Hardware
