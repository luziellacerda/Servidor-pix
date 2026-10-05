# Station atual — 05/10/2026

Leia `docs/station-android/RETORNO-SERVIDOR-NETPLAY-INTERNET-STATION-R12-20261005.md`: APIe4e557a/PID660598, catálogo14/2212, licençaPOCO privada pronta,512salas configuradas e256conexões reais verificadas. API/Nginxlocal p95<1,1ms; WSSexterno p95>2s exige diagnóstico. Preserve appR27/retorno4fd2231 e outros produtos. Capacidade configurada não comprova gameplay homologado. Scripts históricos recusam versão posterior.

# Servidor-pix em servidor compartilhado

Antes de trabalhar na integração da TurboramaStation Android, leia integralmente `docs/station-android/AGENTS.md`, `docs/station-android/README.md`, `docs/station-android/INVENTARIO-SERVIDOR.md` e `docs/station-android/PLANO-INTEGRACAO.md`. O handoff original do aplicativo está vinculado no README. Execute `bash docs/station-android/scripts/inventario-somente-leitura.sh` para conferir o estado atual; o inventário do guia é datado.

O servidor atende outros produtos em produção. Preserve PIX, Suite Windows, EmulationStation Windows, site, WhatsApp, gateway, painel, bancos, dados dos clientes e demais serviços. Integre o Android de forma aditiva e isolada; não trate a leitura do guia como autorização para implantação. Não altere unidades systemd, Nginx, Cloudflare, firewall, portas, `/opt`, `/etc`, `/var/lib`, bancos, worktrees existentes ou APK em consequência deste guia. Confirme separadamente o alvo e o plano de retorno antes de qualquer mudança de produção.

Mantenha segredos e dados pessoais fora dos relatórios e commits. Use testes e contas sintéticos. Se o estado observado divergir do guia, investigue antes de codificar ou implantar.
