# Status do servidor Linux — TurboRama PIX

Atualizado em: 2026-08-07  
Servidor: `lz-servidor-A520M-S2H`  
Diretório de trabalho: `/home/lz-servidor/turborama-download/Servidor-pix`

## Resumo executivo

O pacote correto foi localizado e verificado, o inventário do servidor foi concluído e um backup
root-only verificável das configurações existentes foi criado antes de qualquer instalação. O ASP.NET
Core Runtime 8 foi instalado e o TurboRama PIX foi implantado como serviço separado, escutando somente
em `127.0.0.1:5187`.

O serviço local está ativo, habilitado e saudável. O nginx e o Cloudflare Tunnel existentes continuam
ativos. O hostname público `pix.lzgames.com.br` foi associado ao serviço HTTP
`http://127.0.0.1:5187` pela Cloudflare e o endpoint público de saúde foi validado com sucesso. Nenhuma
credencial Mercado Pago foi configurada e nenhuma cobrança real foi executada.

## Sistema operacional e arquitetura

- Distribuição: Ubuntu 24.04.4 LTS (Noble Numbat)
- Arquitetura: `x86_64`
- Espaço em `/` no inventário inicial: 220 GiB no total, 106 GiB usados e 103 GiB disponíveis (51%)
- Memória no inventário inicial: 7,7 GiB total, aproximadamente 3,6 GiB disponíveis
- Swap: 4,0 GiB

## Versões instaladas

- Git: `2.43.0`
- cloudflared: `2026.7.3` (build `2026-07-23-09:58 UTC`)
- Host .NET: `8.0.29`, arquitetura x64, RID `ubuntu.24.04-x64`
- `Microsoft.NETCore.App`: `8.0.29`
- `Microsoft.AspNetCore.App`: `8.0.29`
- SDK .NET: não instalado; não é necessário para executar o pacote portátil

Pacotes instalados nesta implantação:

- `dotnet-host-8.0` `8.0.29-0ubuntu1~24.04.1`
- `dotnet-hostfxr-8.0` `8.0.29-0ubuntu1~24.04.1`
- `dotnet-runtime-8.0` `8.0.29-0ubuntu1~24.04.1`
- `aspnetcore-runtime-8.0` `8.0.29-0ubuntu1~24.04.1`

Todos aparecem no `dpkg` como `install ok installed`.

## Serviços

Estado confirmado ao final:

| Serviço | Ativo | Habilitado no boot | Observação |
|---|---:|---:|---|
| `cloudflared` | sim | sim | Existente; configuração preservada e validada |
| `nginx` | sim | sim | Existente; preservado |
| `apache2` | não | não verificado | Inativo |
| `caddy` | não | não verificado | Inativo/não carregado |
| `turborama-pix` | sim | sim | Novo serviço isolado |

## Portas TCP ocupadas

Levantamento final com `ss -lnt`:

| Endereço | Porta | Observação conhecida |
|---|---:|---|
| `0.0.0.0` | 22 | SSH |
| `0.0.0.0` | 80 | nginx/site existente |
| `0.0.0.0` | 443 | nginx/site existente |
| `0.0.0.0` e `[::]` | 139 | serviço preexistente |
| `0.0.0.0` e `[::]` | 445 | serviço preexistente |
| `0.0.0.0` | 3306 | serviço preexistente |
| `*` | 8083 | aplicação preexistente (`MainThread` no inventário inicial) |
| `127.0.0.1` | 5432 | serviço preexistente |
| `127.0.0.1` e `[::1]` | 6379 | serviço preexistente |
| `127.0.0.1` e `[::1]` | 11211 | serviço preexistente |
| `127.0.0.1` e `[::1]` | 631 | serviço preexistente |
| `127.0.0.1` | 20241 | serviço preexistente |
| loopback local | 53 | resolvedor do sistema |
| `127.0.0.1` | 5187 | **TurboRama PIX**, adicionada nesta implantação |

A porta planejada `5187` estava livre antes da implantação. O TurboRama não abriu porta pública.

## Pacote utilizado

- Caminho: `/home/lz-servidor/turborama-download/Servidor-pix/outputs/TurboRamaPixOnlineServer-portable-52c8847.zip`
- Tamanho exibido: 56 KiB
- SHA-256 verificado:
  `71af50e4d837d0e831277e4edd16fdedf1f242acb8f27b338b7ed17a275f994a`
- Commit do repositório confirmado: `cee2127a8ff647c17537105bec209fc63758a4b4`
- Branch: `main`

O SHA-256 externo corresponde exatamente ao exigido pelo handoff. Os quatro arquivos internos também
passaram no manifesto `CHECKSUMS-SHA256.txt` antes do autoteste e novamente durante a instalação.

## Backup realizado

Backup criado antes de instalar o serviço e antes de qualquer possível alteração no Tunnel:

`/var/backups/turborama-pix/preinstall-20260807T105905Z`

Características:

- diretório root-only, com permissões removidas para grupo e outros;
- cópia de `/etc/cloudflared`;
- cópia de `/etc/nginx`;
- cópia da unidade systemd existente do cloudflared;
- registro do estado dos serviços e das portas em escuta;
- resultado da validação da configuração do Tunnel;
- resultado de `nginx -t`;
- manifesto `SHA256SUMS`;
- verificação posterior de todos os itens do manifesto: **OK**.

O backup contém material sensível e não deve ser copiado para chat, Git ou local sem proteção adequada.

## Arquivos e recursos criados ou alterados

### Criados no sistema

- `/opt/turborama-pix/`
  - binário `.dll` e metadados `.json` do pacote verificado;
- `/var/lib/turborama-pix/`
  - diretório privado de estado, proprietário `turborama-pix:turborama-pix`, modo `0700`;
- `/etc/turborama-pix/`
  - diretório privado root-only, modo `0700`;
- `/etc/turborama-pix/server.env`
  - arquivo privado, proprietário `root:root`, modo `0600`;
  - contém configuração e chaves aleatórias geradas localmente; seu conteúdo não foi exibido;
- `/etc/systemd/system/turborama-pix.service`
  - nova unidade systemd, modo `0644`;
- `/etc/systemd/system/multi-user.target.wants/turborama-pix.service`
  - link criado por `systemctl enable`;
- usuário de sistema dedicado `turborama-pix`, sem shell de login;
- `/var/backups/turborama-pix/preinstall-20260807T105905Z`;
- scripts operacionais locais:
  - `/home/lz-servidor/turborama-download/Servidor-pix/ops/create-preinstall-backup.sh`;
  - `/home/lz-servidor/turborama-download/Servidor-pix/ops/install-turborama-pix.sh`;
- este arquivo:
  - `/home/lz-servidor/turborama-download/Servidor-pix/STATUS-SERVIDOR-LINUX.md`.

### Alterados pelo gerenciador de pacotes/systemd

- banco do `dpkg/apt`, pela instalação dos quatro pacotes .NET listados acima;
- estado do systemd, por `daemon-reload` e habilitação do novo serviço.

### Explicitamente não alterados

- `/etc/cloudflared/config.yml`;
- unidade systemd do cloudflared;
- configurações do nginx;
- site existente;
- arquivos locais de configuração do Cloudflare Tunnel (o hostname foi configurado pelo painel da
  Cloudflare);
- credenciais do Cloudflare.

O repositório está em `main`; `git status` mostra `ops/` como não rastreado. Este handoff também passa a
ser um arquivo não rastreado após sua criação. Nada foi commitado ou enviado ao GitHub.

## Estado do TurboRama PIX

- Unidade: `turborama-pix.service`
- Estado: `active`
- Boot: `enabled`
- Usuário/grupo: `turborama-pix:turborama-pix`
- Aplicação: `/opt/turborama-pix/TurboRamaPixOnlineServer.dll`
- Estado: `/var/lib/turborama-pix/state.json` quando houver dados persistidos
- Escuta: `http://127.0.0.1:5187`
- Ambiente: `Production`
- Endpoint local: `http://127.0.0.1:5187/v1/health`
- Resposta final: `{"schemaVersion":1,"ready":true,"service":"turborama-online"}`
- Credencial Mercado Pago: **não configurada**
- Cobrança real: **não executada**

A unidade aplica isolamento, incluindo `NoNewPrivileges`, `PrivateTmp`, `ProtectSystem=strict`,
`ProtectHome=true`, `UMask=0077` e escrita limitada ao diretório de estado.

## Testes aprovados

- SHA-256 do ZIP externo: aprovado;
- teste estrutural do ZIP: aprovado;
- checksums internos do pacote: aprovados;
- autoteste sem conexão financeira e sem cobrança real: aprovado;
- resultado do autoteste:
  `SELF-TEST SERVIDOR ONLINE: OK`;
- instalação e detecção dos runtimes .NET 8.0.29: aprovadas;
- inicialização do serviço systemd: aprovada;
- bind exclusivo em `127.0.0.1:5187`: aprovado;
- endpoint local `/v1/health`: aprovado;
- endpoint público `https://pix.lzgames.com.br/v1/health`: aprovado;
- configuração atual do Cloudflare Tunnel: `OK`;
- conector do túnel `lz-fix` ativo nos edges da Cloudflare;
- nginx e cloudflared permaneceram ativos após a instalação;
- manifesto SHA-256 do backup: todos os arquivos `OK`;
- `dpkg --audit`: sem saída e código zero.

## Erros, avisos e limitações encontrados

1. O sandbox inicial falhou algumas vezes com
   `bwrap: loopback: Failed RTM_NEWADDR: Operation not permitted`. Os comandos foram repetidos com
   autorização fora do sandbox; isso não alterou o servidor.
2. `sudo -n` não reutilizou a autenticação do terminal e informou que uma senha era necessária. O acesso
   administrativo passou a ser feito por confirmação gráfica com `pkexec`; nenhuma senha foi enviada
   pelo chat.
3. Uma primeira tentativa de validar o Tunnel usou a posição errada de `--config`. Foi apenas um erro de
   sintaxe, sem efeito. A ordem correta foi executada depois e retornou `OK`.
4. Ao final da instalação do runtime, um gatilho preexistente de `libdvd-pkg` informou que seu
   `apt-get check` falhou. Os quatro pacotes .NET foram instalados corretamente e `dpkg --audit` ficou
   limpo. Uma tentativa posterior de `apt-get check` sem privilégios falhou apenas por não conseguir
   abrir o lock do `dpkg`. Ainda é recomendado executar `apt-get check` via `pkexec` antes da próxima
   alteração.
5. O inventário inicial de processos com `sudo ss -lntp` não pôde usar sudo não interativo. `ss -lntp`
   sem root e, ao final, `ss -lnt` confirmaram as portas; nomes de processos pertencentes a outros
   usuários ficaram ocultos.

## Principais comandos executados

Os comandos abaixo são apresentados sem qualquer valor secreto:

```bash
# Leitura do handoff
find '/media/lz-servidor/STORY INFOR' -maxdepth 3 -name HANDOFF-CONTINUAR-NO-LINUX.md -type f -print
sed -n '1,260p' '/media/lz-servidor/STORY INFOR/HANDOFF-CONTINUAR-NO-LINUX.md'

# Pacote e Git
ls -lh outputs/TurboRamaPixOnlineServer-portable-52c8847.zip
sha256sum outputs/TurboRamaPixOnlineServer-portable-52c8847.zip
unzip -l outputs/TurboRamaPixOnlineServer-portable-52c8847.zip
unzip -qq -t outputs/TurboRamaPixOnlineServer-portable-52c8847.zip
git rev-parse HEAD
git status --short --branch

# Inventário
cat /etc/os-release
uname -m
git --version
dotnet --info
cloudflared --version
systemctl is-active cloudflared
systemctl is-enabled cloudflared
systemctl is-active nginx
systemctl is-active apache2
systemctl is-active caddy
ss -lntp
ss -lnt
df -h /
free -h

# Inspeção sanitizada e validação dos serviços existentes
systemctl show cloudflared -p FragmentPath -p DropInPaths --value
systemctl show nginx -p FragmentPath -p DropInPaths --value
find /etc/nginx -maxdepth 3 -type f -printf '%m %u:%g %p\n'
cloudflared tunnel --config /etc/cloudflared/config.yml ingress validate

# Runtime
apt-cache policy aspnetcore-runtime-8.0 dotnet-runtime-8.0
pkexec /usr/bin/apt-get install --yes aspnetcore-runtime-8.0
dpkg --audit
dpkg-query -W dotnet-host-8.0 dotnet-hostfxr-8.0 dotnet-runtime-8.0 aspnetcore-runtime-8.0

# Autoteste sem cobrança
unzip -o outputs/TurboRamaPixOnlineServer-portable-52c8847.zip -d /tmp/turborama-pix-selftest-52c8847
sha256sum --check CHECKSUMS-SHA256.txt
dotnet TurboRamaPixOnlineServer.dll --self-test

# Backup e instalação isolada
pkexec /usr/bin/bash /home/lz-servidor/turborama-download/Servidor-pix/ops/create-preinstall-backup.sh
pkexec /usr/bin/bash -c 'cd /var/backups/turborama-pix/preinstall-20260807T105905Z && /usr/bin/sha256sum --check SHA256SUMS'
pkexec /usr/bin/bash /home/lz-servidor/turborama-download/Servidor-pix/ops/install-turborama-pix.sh

# Validação final
systemctl is-active turborama-pix
systemctl is-enabled turborama-pix
curl --fail --silent --show-error --max-time 10 http://127.0.0.1:5187/v1/health
ss -lntp 'sport = :5187'
journalctl -u turborama-pix --since '-5 minutes' --no-pager -n 80
systemctl is-active cloudflared nginx
```

Também foram usados filtros de `awk`, `rg`, `sed` e `strings` somente para produzir inventários
sanitizados e identificar nomes de configurações públicas. Nenhum conteúdo secreto foi registrado.

## Registro anterior — publicação ainda pendente (superado)

Instalação local isolada e validação completa do `turborama-pix.service`, incluindo endpoint de saúde,
porta local, logs iniciais e confirmação de que nginx e cloudflared continuaram ativos.

A publicação ainda estava pendente neste ponto do histórico. Este estado foi superado pelas atualizações
de continuidade ao final do documento.

## Plano anterior (superado)

1. Executar `apt-get check` com privilégios para esclarecer o aviso preexistente do `libdvd-pkg`, sem
   executar `autoremove` nem correção automática sem revisão.
2. Obter confirmação explícita para alterar o Tunnel existente.
3. Criar uma cópia adicional imediatamente anterior da configuração do Tunnel, mantendo o backup já
   verificado.
4. Adicionar, antes da regra catch-all, o ingress:

   ```yaml
   - hostname: pix.lzgames.com.br
     service: http://127.0.0.1:5187
   ```

5. Validar a configuração antes de qualquer restart.
6. Criar a rota DNS do novo hostname para o Tunnel existente.
7. Reiniciar somente o cloudflared, verificar que ele voltou a `active` e confirmar que todos os
   hostnames antigos continuam respondendo.
8. Testar `https://pix.lzgames.com.br/v1/health` externamente.
9. Configurar uma credencial Mercado Pago **nova** diretamente no servidor, por entrada oculta, somente
   depois da publicação e das verificações. Não reutilizar credenciais que já apareceram em conversa.
10. Ativar somente um gabinete e fazer um teste real controlado de valor mínimo apenas na fase final.

## Comandos que estavam pendentes nessa etapa histórica

Os exemplos abaixo registram o trabalho pendente. Devem ser revisados e executados somente após a
confirmação explícita e com rollback preparado:

```bash
# Verificação pendente do estado do apt; não corrige nem remove pacotes
pkexec /usr/bin/apt-get check

# Ainda não executado: editar /etc/cloudflared/config.yml para incluir pix.lzgames.com.br
# A edição deve preservar todas as regras existentes e manter a regra catch-all por último.

# Ainda não executado: validação da configuração editada
cloudflared tunnel --config /etc/cloudflared/config.yml ingress validate

# Ainda não executado: criação da rota DNS para o Tunnel existente
# Usar o identificador/nome do Tunnel obtido localmente; não publicar credenciais em chat.
cloudflared tunnel route dns TUNNEL_EXISTENTE pix.lzgames.com.br

# Ainda não executado: restart controlado após validação
pkexec /usr/bin/systemctl restart cloudflared
systemctl is-active cloudflared

# Ainda não executado: teste externo
curl --fail --silent --show-error --max-time 15 https://pix.lzgames.com.br/v1/health

# Ainda não executados:
# - configuração de credencial Mercado Pago nova;
# - criação de licença/código de ativação para produção;
# - ativação de gabinete;
# - cobrança real controlada;
# - commit ou push dos scripts e deste handoff.
```

## Restrições de segurança para a continuidade

- Não exibir nem copiar o conteúdo de `/etc/turborama-pix/server.env`.
- Não exibir arquivos de credenciais do Cloudflare.
- Não solicitar senha, token, Client Secret, chave privada ou Access Token pelo chat.
- Tratar qualquer credencial financeira vista em conversa anterior como exposta e substituí-la.
- Não abrir `5187` publicamente; manter a aplicação apenas em loopback.
- Não executar `apt autoremove` por sugestão automática.
- Não editar nginx ou Tunnel sem backup, validação prévia, confirmação e plano de rollback.
- Não executar cobrança real durante diagnóstico, instalação ou autoteste.

## Atualização de continuidade — diagnóstico da publicação parcial do Tunnel (histórico)

Esta seção registra o trabalho realizado depois da primeira emissão deste handoff e substitui qualquer
estado pendente conflitante descrito anteriormente.

### Ações concluídas

- `pkexec /usr/bin/apt-get check`: aprovado, código zero;
- backup adicional pré-Tunnel criado em
  `/var/backups/turborama-pix/pre-tunnel-20260807T111242Z`;
- tentativa de adicionar localmente
  `pix.lzgames.com.br -> http://127.0.0.1:5187`, validada antes e depois da edição;
- rota DNS CNAME de `pix.lzgames.com.br` criada para o Tunnel existente;
- cloudflared reiniciado com sucesso;
- prechecks do conector para DNS, QUIC, HTTP/2 e API Cloudflare: aprovados;
- `app.lzgames.com.br`, `api.lzgames.com.br` e `pma.lzgames.com.br`: HTTP 200 antes e depois do
  restart;
- cloudflared, nginx e turborama-pix permaneceram ativos;
- arquivo YAML local restaurado a partir do backup e novamente validado com resultado `OK`.

### Diagnóstico realizado nessa etapa

O novo hostname retorna HTTP 404 porque o Tunnel `lz-fix` usa configuração remota do Cloudflare. Após
o restart, o conector recebeu uma versão remota que contém inclusive rotas adicionais ausentes no YAML
local. Essa configuração remota substitui os ingress locais.

Portanto, editar `/etc/cloudflared/config.yml` não publica a aplicação nesse Tunnel. A edição local foi
desfeita para evitar divergência e não houve novo restart depois da restauração. O DNS criado permanece
apontando ao Tunnel e ficará funcional quando a rota remota for adicionada.

O cloudflared também registrou aviso preexistente de ICMP proxy desabilitado por `ping_group_range`.
Isso não afetou HTTP/HTTPS e todos os testes de conectividade relevantes passaram.

### Arquivos adicionais criados

- `ops/add-cloudflared-pix-ingress.sh`;
- `ops/rollback-cloudflared-pix-ingress.sh`;
- `/var/backups/turborama-pix/pre-tunnel-20260807T111242Z`.

### Comandos adicionais executados

```bash
pkexec /usr/bin/apt-get check
pkexec /usr/bin/bash /home/lz-servidor/turborama-download/Servidor-pix/ops/add-cloudflared-pix-ingress.sh
cloudflared tunnel route dns TUNNEL_EXISTENTE pix.lzgames.com.br
pkexec /usr/bin/systemctl restart cloudflared
cloudflared tunnel info TUNNEL_EXISTENTE
journalctl -u cloudflared --since '-3 minutes' --no-pager -n 100
curl --fail --silent --show-error --max-time 20 https://pix.lzgames.com.br/v1/health
pkexec /usr/bin/install -m 0644 -o root -g root \
  /var/backups/turborama-pix/pre-tunnel-20260807T111242Z/config.yml.before-pix \
  /etc/cloudflared/config.yml
cloudflared tunnel --config /etc/cloudflared/config.yml ingress validate
```

### Estado ao final dessa etapa histórica

DNS criado, cloudflared reiniciado e validado, sites existentes confirmados com HTTP 200, gerenciamento
remoto diagnosticado e YAML local restaurado. O endpoint local do TurboRama continua saudável em
`127.0.0.1:5187`; externamente, `pix.lzgames.com.br` ainda retorna 404.

### Orientação emitida nessa etapa histórica

No painel Cloudflare Zero Trust, abrir o Tunnel `lz-fix` e adicionar uma aplicação publicada com:

- hostname: `pix.lzgames.com.br`;
- serviço: `http://127.0.0.1:5187`.

Todas as rotas remotas existentes devem ser preservadas. O DNS já existe. Depois de salvar, executar:

```bash
curl --fail --silent --show-error --max-time 15 https://pix.lzgames.com.br/v1/health
systemctl is-active cloudflared nginx turborama-pix
```

Também devem ser revalidados os hostnames antigos. Continuam não executados: configuração Mercado Pago,
criação de licença/código de produção, ativação de gabinete e cobrança real controlada.

## Atualização final — publicação externa concluída

Em 2026-08-07, a rota remota foi adicionada pelo painel Cloudflare Zero Trust com:

- hostname: `pix.lzgames.com.br`;
- serviço de origem: `http://127.0.0.1:5187`;
- tipo: HTTP.

Validação final:

- `turborama-pix`, `cloudflared` e `nginx`: ativos e habilitados no boot;
- configuração YAML local do Tunnel: validada com resultado `OK` e preservada;
- Tunnel `lz-fix` (`fe557774-52a2-42c6-9b13-56321562dd0f`): conector ativo nos edges da Cloudflare;
- `http://127.0.0.1:5187/v1/health`: HTTP bem-sucedido, `ready: true`;
- `https://pix.lzgames.com.br/v1/health`: HTTP bem-sucedido, `ready: true`.

A publicação externa está concluída. Permanecem pendentes somente as etapas de produção: configurar
uma credencial nova do Mercado Pago diretamente no servidor, criar licença/código de ativação, ativar
um gabinete e realizar uma cobrança real controlada. Nenhuma credencial financeira foi exibida e
nenhuma cobrança real foi executada durante esta implantação.
