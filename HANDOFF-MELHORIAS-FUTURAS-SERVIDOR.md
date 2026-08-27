# Handoff — melhorias futuras do servidor LZGames

Atualizado em: 2026-08-07  
Servidor: `lz-servidor-A520M-S2H`  
Sistema: Ubuntu 24.04.4 LTS  
Objetivo: melhorar segurança, confiabilidade e desempenho sem perder funcionalidades existentes.

## Regra principal

Este documento resulta de uma auditoria somente leitura. Nenhum serviço, arquivo, pacote, regra de
firewall ou configuração foi alterado durante a análise.

Antes de qualquer mudança futura:

1. identificar todas as dependências da configuração;
2. criar backup verificável;
3. registrar o estado e os testes anteriores;
4. alterar somente um conjunto pequeno por vez;
5. validar as funcionalidades antigas e novas;
6. manter rollback pronto.

Não aplicar todas as recomendações de uma só vez.

## Estado geral observado

- CPU: AMD Ryzen 5 2600X, 6 núcleos e 12 threads;
- memória: 7,7 GiB, aproximadamente 3,2 GiB disponíveis durante a análise;
- swap: 4 GiB, somente cerca de 8,5 MiB usados;
- raiz: SSD Western Digital de 240 GB, 51% ocupado e aproximadamente 102 GB livres;
- rede: Ethernet 1 Gbit/s, full duplex;
- carga: baixa, CPU aproximadamente 97% ociosa na amostragem;
- I/O: sem saturação ou latência relevante na amostragem;
- UFW, Fail2Ban e AppArmor: ativos;
- `turborama-pix`, `cloudflared`, `nginx`, MariaDB, PostgreSQL, Redis, Memcached, PHP-FPM e
  `lzgames-api`: ativos;
- endpoint PIX local e público: saudáveis;
- único serviço systemd em estado failed: `certbot.service`.

Não há justificativa atual para aumentar CPU, RAM, swap, buffers do banco ou limites do nginx.

## Prioridade 1 — renovar e estabilizar o certificado local

Achados:

- certificado local de `app.lzgames.com.br` expirou em 2026-03-19;
- Certbot tenta renovar aproximadamente duas vezes ao dia e falha em todas as tentativas;
- método atual: ACME `webroot`;
- webroot configurado: `/var/www/html`;
- nginx usa o certificado em
  `/etc/letsencrypt/live/app.lzgames.com.br/fullchain.pem`;
- a configuração nginx é sintaticamente válida quando testada como root;
- Cloudflare pode manter o acesso externo funcionando, mas não corrige o certificado local expirado.

Plano seguro:

1. copiar para backup `/etc/letsencrypt` e as configurações nginx;
2. verificar o acesso público a
   `http://app.lzgames.com.br/.well-known/acme-challenge/ARQUIVO-DE-TESTE`;
3. confirmar se a Cloudflare encaminha esse caminho ao nginx correto;
4. revisar se o bloco HTTP de `app.lzgames.com.br` serve o webroot esperado;
5. decidir entre corrigir o método webroot ou migrar para DNS-01;
6. executar primeiro um teste de renovação;
7. validar o certificado e executar `nginx -t`;
8. recarregar nginx somente após validação;
9. testar todos os hostnames antigos e o PIX.

Não apagar o certificado antigo antes de emitir e validar o novo.

## Prioridade 2 — restringir MariaDB sem interromper clientes

Achados:

- MariaDB 12.3.2 escuta em `0.0.0.0:3306`;
- UFW libera 3306 globalmente em IPv4 e IPv6;
- há regras redundantes para 3306 e 13306;
- 13306 não tinha processo escutando durante a análise;
- também existe uma regra específica de 3306 para `82.25.96.190`;
- pico observado: 19 conexões, limite 151;
- buffer pool: 1 GiB;
- sem consultas lentas registradas;
- alguns timeouts e conexões abortadas aparecem nos logs;
- não havia conexão TCP remota ativa no instante da verificação.

Plano seguro:

1. levantar por logs, aplicação e responsáveis todos os clientes externos;
2. verificar se o IP específico ainda é legítimo e estável;
3. criar exportação lógica e backup físico/configuracional verificáveis;
4. remover primeiro apenas regras duplicadas comprovadamente equivalentes;
5. restringir acesso a IPs conhecidos, VPN ou túnel privado;
6. manter uma sessão administrativa aberta durante a mudança;
7. testar APIs, phpMyAdmin, sites, rotinas e clientes externos;
8. somente depois avaliar mudança de `bind-address`.

Não alterar simultaneamente firewall, usuários do banco e `bind-address`.

## Prioridade 3 — endurecer SSH gradualmente

Estado observado:

- porta 22 em IPv4 e IPv6;
- autenticação por senha habilitada;
- autenticação por chave habilitada;
- root permitido por chave;
- `X11Forwarding yes`;
- `MaxAuthTries 6`;
- Fail2Ban ativo com jail `sshd`.

Plano seguro:

1. confirmar quais usuários e sistemas dependem de senha ou X11;
2. instalar e testar chaves para todos os administradores;
3. abrir uma segunda sessão SSH e mantê-la conectada;
4. restringir root, senha, usuários permitidos e X11 em etapas separadas;
5. validar cada etapa antes de fechar a sessão anterior;
6. revisar IPv6 explicitamente, pois ele também está liberado no firewall.

Nunca desabilitar senha antes de comprovar login por chave em uma nova sessão.

## Prioridade 4 — unidade removível e integridade de dados

Os logs registraram em unidades removíveis FAT:

- desmontagem incorreta;
- `Buffer I/O error`;
- `lost async page write`;
- dispositivo ficando offline durante gravação.

Isso pode estar relacionado à unidade `STORY INFOR`.

Plano seguro:

1. copiar os dados importantes para outro armazenamento;
2. desmontar corretamente a unidade;
3. verificar cabo, porta USB e alimentação;
4. executar verificação do sistema de arquivos somente desmontado;
5. substituir a mídia se os erros reaparecerem.

Não executar `fsck` em volume montado.

## Atualizações pendentes

Foram observadas aproximadamente 23 atualizações, incluindo:

- Docker Engine, CLI, Buildx, Compose e containerd;
- Node.js;
- NetworkManager;
- Chrome;
- Apport e componentes de desktop.

Plano seguro:

1. criar backup e registrar versões;
2. executar `apt-get check`;
3. atualizar em grupos, começando pelos componentes de menor impacto;
4. reservar Docker, Node.js e NetworkManager para janela controlada;
5. verificar necessidade de reboot;
6. testar rede, SSH, bancos, nginx, Cloudflare, API, sites e PIX.

Não executar `autoremove` automaticamente.

## Oportunidades de desempenho e simplificação

Estas ações são opcionais e exigem confirmação de uso:

- Docker está ativo sem contêineres e possui uma imagem não utilizada de aproximadamente 1,8 GiB;
- Redis e Memcached estavam praticamente sem atividade;
- Avahi, CUPS, ModemManager, Samba, Wi-Fi, interface gráfica e serviços desktop podem ser desnecessários
  em um servidor dedicado;
- Firefox, Chrome e GNOME são os maiores consumidores de memória;
- journal ocupa aproximadamente 1 GiB;
- boot total observado: aproximadamente 29,5 segundos, com maior tempo em tarefas apt e espera de rede.

Não desativar nenhum desses componentes somente por parecer ocioso. Primeiro confirmar dependências e
uso real com os responsáveis.

## Bancos e aplicações

- MariaDB: não aumentar `max_connections` nem buffer pool sem evidência de saturação;
- investigar os timeouts dos clientes e pools das aplicações antes de ajustar timeouts do servidor;
- PostgreSQL escuta apenas em localhost e tinha seis sessões;
- Redis e Memcached escutam somente em loopback;
- PHP-FPM tinha dois workers ociosos, sem solicitações lentas;
- `lzgames-api` apresentava tokens expirados e algumas tentativas de senha inválida nos logs;
- TurboRama PIX consumia cerca de 36 MiB e respondia normalmente;
- Cloudflare Tunnel tinha quatro conexões QUIC e a rota PIX remota ativa.

## Monitoramento recomendado

Em futura janela de manutenção, considerar:

- instalar `smartmontools` para saúde SMART dos três discos;
- instalar `lm-sensors` para temperaturas;
- definir alertas para certificado, disco, uso de espaço, serviço failed e endpoint HTTP;
- acompanhar conexões abortadas e consultas lentas do MariaDB;
- definir retenção explícita para systemd-journald;
- testar restauração dos backups, não apenas criação.

O firmware da placa-mãe é de 2022 e o sistema usa boot legado sem ESP UEFI. Atualização de BIOS deve ser
tratada como projeto separado, com verificação de compatibilidade, energia estável e plano de recuperação.

## Testes obrigatórios após qualquer mudança

- `systemctl --failed`;
- estado de nginx, Cloudflare, MariaDB, PostgreSQL, Redis, Memcached, PHP-FPM, API e TurboRama;
- `nginx -t`;
- portas esperadas com `ss -lntup`;
- login SSH por uma nova sessão;
- sites `app`, `api`, `pma`, `ponto` e `suporte`;
- `https://pix.lzgames.com.br/v1/health`;
- APIs e autenticação;
- acesso legítimo ao MariaDB;
- logs de erro após a mudança;
- comparação com o inventário anterior.

## Ordem recomendada de execução

1. corrigir certificado e Certbot;
2. mapear e restringir exposição do MariaDB;
3. endurecer SSH em etapas;
4. proteger e verificar a unidade removível;
5. aplicar atualizações em janela controlada;
6. instalar monitoramento de discos e temperaturas;
7. revisar serviços opcionais;
8. otimizar somente após medir carga real por período maior.

## Estado deste handoff

Documento de planejamento apenas. Nenhuma das modificações recomendadas foi executada durante a
auditoria. Qualquer execução futura exige novo backup, confirmação de escopo e validação funcional.
