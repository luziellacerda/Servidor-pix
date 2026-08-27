# Handoff completo — TurboRama no servidor

Atualizado em: 2026-08-21 (America/Maceio)  
Servidor: `lz-servidor-A520M-S2H`  
Sistema: TurboRama PIX Online Server  
Repositório local: `/home/lz-servidor/turborama-download/Servidor-pix`  
Implantação ativa: `/opt/turborama-pix`

## 1. Resumo executivo

O TurboRama PIX está em produção, saudável e habilitado no boot. A aplicação ASP.NET Core/.NET 8
escuta exclusivamente em `127.0.0.1:5187`; o acesso externo passa pela Cloudflare. A API pública está
em `https://pix.lzgames.com.br` e o painel administrativo em
`https://painelpix.lzgames.com.br/admin`, protegido primeiro pelo Cloudflare Access e depois pelo login
próprio da aplicação.

Estado verificado em 2026-08-21:

- `turborama-pix.service`: ativo, habilitado, zero reinícios desde 2026-08-20 09:14:50 -03;
- `nginx`: ativo e habilitado;
- `cloudflared`: ativo e habilitado;
- saúde local: HTTP `200`;
- saúde pública HTTPS: HTTP `200`, com HSTS de um ano;
- API pública por HTTP simples: `400`, portanto não serve conteúdo operacional sem HTTPS;
- `/admin` no hostname da API: `404`;
- painel administrativo: redireciona para Cloudflare Access (`302`);
- autoteste interno: aprovado;
- não há alertas do serviço desde 2026-08-20.

Há uma pendência crítica de rastreabilidade: a DLL ativa é uma compilação posterior ao pacote R13 e
não corresponde ao manifesto `CHECKSUMS-SHA256.txt` atualmente instalado. O serviço funciona e o
autoteste passa, mas a origem exata dessa compilação precisa ser consolidada em um commit e pacote
imutável antes do próximo deploy.

## 2. Função e limites do sistema

O servidor é responsável por:

- licenças e seus estados;
- ativação por código de uso único;
- vínculo e transferência controlada de hardware;
- desafios criptográficos e prova RSA-PSS-SHA256;
- perfis `TPM_BOUND` e `SOFTWARE_BOUND_ONLINE`;
- sessão exclusiva, anti-replay e registro de tentativa de clonagem;
- bloqueio remoto de novas compras PIX por licença;
- configuração remota autorizada do gabinete;
- painel administrativo, auditoria e exportação CSV.

Continuam no gabinete, não neste servidor:

- credencial e integração Mercado Pago usada no fluxo atual;
- criação e consulta da cobrança real;
- PDV, QR Code, preços operacionais e concessão de créditos;
- continuidade dos jogos e créditos durante indisponibilidade de rede.

Uma falha de rede ou erro `5xx` não deve remover créditos nem encerrar jogos. Somente uma recusa
explícita, autenticada e válida pode impedir uma nova operação.

## 3. Arquitetura em produção

```text
Gabinete TurboRama
    |
    | HTTPS / protocolo assinado
    v
Cloudflare (DNS, TLS e borda)
    |
    +-- pix.lzgames.com.br ---------> API /v1/*
    |
    +-- painelpix.lzgames.com.br ---> Cloudflare Access ---> /admin
                                         |
                                         v
                              cloudflared no servidor
                                         |
                                         v
                              http://127.0.0.1:5187
                                         |
                                         v
                              turborama-pix.service
                                         |
                     +-------------------+------------------+
                     |                                      |
          /var/lib/turborama-pix                  /etc/turborama-pix
          estado persistente                       configuração secreta
```

Não existe porta pública própria do TurboRama. A porta `5187` deve permanecer vinculada somente ao
loopback.

## 4. Componentes, caminhos e permissões

| Item | Caminho/valor |
|---|---|
| Serviço | `/etc/systemd/system/turborama-pix.service` |
| Executável | `/usr/bin/dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll` |
| Diretório de trabalho | `/opt/turborama-pix` |
| Configuração secreta | `/etc/turborama-pix/server.env` |
| Estado persistente | `/var/lib/turborama-pix` |
| Usuário/grupo | `turborama-pix:turborama-pix` (`uid=997`, `gid=982`) |
| Bind | `127.0.0.1:5187` |
| API pública | `https://pix.lzgames.com.br` |
| Painel | `https://painelpix.lzgames.com.br/admin` |
| Runtime | .NET/ASP.NET Core 8.0.30 x64 |

Permissões confirmadas:

- `/etc/turborama-pix`: `0700 root:root`;
- `/var/lib/turborama-pix`: `0700 turborama-pix:turborama-pix`;
- arquivos da aplicação: `root:root`, leitura pública (`0644`);
- o conteúdo do ambiente e do estado não foi lido nem copiado para este documento.

A unidade systemd usa `NoNewPrivileges=true`, `PrivateTmp=true`, `ProtectSystem=strict`,
`ProtectHome=true`, `UMask=0077` e libera escrita apenas em `/var/lib/turborama-pix`.

## 5. Configuração obrigatória

O arquivo `/etc/turborama-pix/server.env` é obrigatório e root-only. Variáveis esperadas pelo código:

```text
ASPNETCORE_URLS
ASPNETCORE_ENVIRONMENT
TURBORAMA_SERVER_STATE_FILE
TURBORAMA_SERVER_STATE_KEY
TURBORAMA_SERVER_SECRET_KEY
TURBORAMA_PAYMENT_EXPIRATION_MINUTES
TURBORAMA_ALLOW_HTTP_LOOPBACK
TURBORAMA_ADMIN_USERNAME
TURBORAMA_ADMIN_PASSWORD_HASH
TURBORAMA_ADMIN_PUBLIC_HOST
TURBORAMA_ADMIN_KEY_DIRECTORY
```

Regras:

- `TURBORAMA_SERVER_STATE_KEY` e `TURBORAMA_SERVER_SECRET_KEY` precisam ser chaves Base64 diferentes,
  cada uma representando exatamente 32 bytes;
- não registrar senha em texto: usar somente o hash PBKDF2 gerado pelo executável;
- `TURBORAMA_ALLOW_HTTP_LOOPBACK=true` é aceitável apenas porque o proxy/túnel acessa o Kestrel pelo
  loopback do mesmo servidor;
- o hostname administrativo deve ser exatamente `painelpix.lzgames.com.br` na configuração produtiva;
- nunca enviar `server.env`, estado, chaves ou backup por chat ou Git.

Para gerar um novo hash de senha administrativa, execute o modo próprio do binário em terminal
seguro e interativo:

```bash
dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll --hash-admin-password
```

Depois de atualizar o arquivo privado, reinicie somente o serviço TurboRama e valide todas as
superfícies descritas na seção 10.

## 6. API e painel

Rotas operacionais da API:

| Método | Rota | Finalidade |
|---|---|---|
| GET | `/v1/health` | prontidão |
| POST | `/v1/activations/challenge` | desafio de ativação |
| POST | `/v1/activations/complete` | prova e conclusão da ativação |
| POST | `/v1/challenges` | desafio de sessão |
| POST | `/v1/sessions` | abertura/validação de sessão |
| POST | `/v1/orders` | compatibilidade de criação de ordem |
| POST | `/v1/orders/status` | compatibilidade de consulta de ordem |
| POST | `/v1/configuration/read` | leitura autenticada de configuração |
| POST | `/v1/configuration/write` | escrita autenticada de configuração |

Superfície administrativa:

- `/admin`, `/admin/login`, `/admin/logout`;
- `/admin/export/audit.csv`;
- ações de licença, dispositivo, PIX, reautenticação e transferência;
- criação de licença e código de ativação;
- limpeza seletiva de auditoria, presente na compilação local ativa.

O painel exige hostname administrativo, HTTPS reconhecido pelo proxy confiável, Cloudflare Access,
cookie `Secure`, `HttpOnly`, `SameSite=Strict`, sessão curta e antifalsificação nas mutações. Ações
críticas exigem confirmação adicional da senha.

## 7. Estado do código e da versão ativa

Repositório:

- branch atual: `SEGURANCA-V2-LOCAL-20260814`;
- HEAD: `82ecef3` (`Incluir pacote validado do servidor R13`);
- remoto da branch no mesmo commit;
- `main`/`origin/main`: `a3bb0f2`;
- árvore de trabalho não está limpa.

Alterações rastreadas não commitadas:

- `src/TurboRamaPixOnlineServer/AdminPanel.cs`;
- `src/TurboRamaPixOnlineServer/ServerCore.cs`.

Essas mudanças adicionam/reorganizam o painel em modais, separação de eventos de segurança e
auditoria e limpeza seletiva do histórico. Existem também documentos, designs, retornos e scripts de
operação não rastreados. Não apagar nem sobrescrever esses arquivos.

Binário ativo em 2026-08-21:

```text
Arquivo: /opt/turborama-pix/TurboRamaPixOnlineServer.dll
Tamanho: 418816 bytes
SHA-256: 0ff7568511470f59e13899dba6d09efa6c0526ed4ed4d2ca6d3fd4b5fa2d4cbb
```

Esse hash não aparece nos backups legíveis inventariados. O manifesto instalado ainda exige a DLL
R13 com SHA-256 `fded6fbc...`, por isso `sha256sum --check` falha somente para a DLL. Os quatro
arquivos auxiliares correspondem ao manifesto. Antes de qualquer atualização:

1. preservar a DLL ativa e gerar um manifesto novo do conjunto ativo;
2. vincular a fonte exata a um commit revisado;
3. gerar pacote imutável com hash externo;
4. executar autoteste;
5. só então considerar a versão uma release reproduzível.

Não reinstalar automaticamente o R13 apenas para corrigir o manifesto: isso removeria recursos
posteriores atualmente ativos.

## 8. Backups conhecidos

Há backups locais em `/home/lz-servidor/backups/turborama-*`, incluindo estágios R13, painel final,
modais e refinamentos posteriores. Alguns subdiretórios de estado e configuração são root-only, como
esperado.

Backup R13 documentado:

```text
/home/lz-servidor/backups/turborama-pix-r13-20260814-122451
```

Backup de aplicação R13/painel final legível:

```text
/home/lz-servidor/backups/turborama-pix-panel-final-20260814-1704/application
DLL SHA-256: fded6fbc4488254bf53542ddbdeeb17c9c2aa651d67bffadf697e0ac7bf0f6d9
```

Esses backups são anteriores à DLL ativa atual. Eles servem como rollback conhecido, mas podem
remover funcionalidades recentes. Antes de restaurar, faça novo backup atômico de:

- `/opt/turborama-pix`;
- `/var/lib/turborama-pix`;
- `/etc/turborama-pix`;
- `/etc/systemd/system/turborama-pix.service`;
- configurações relevantes do Cloudflare e proxy, sem copiá-las para locais inseguros.

O estado e as duas chaves precisam ser preservados juntos. Restaurar estado sem as chaves corretas
torna os dados inutilizáveis; restaurar chaves sem o estado correto não reconstrói licenças.

## 9. Operação diária

Status e logs:

```bash
systemctl status turborama-pix --no-pager --full
systemctl is-active turborama-pix nginx cloudflared
systemctl is-enabled turborama-pix nginx cloudflared
journalctl -u turborama-pix --since '-30 minutes' --no-pager
ss -lntp | rg ':5187'
```

Saúde:

```bash
curl --fail --silent --show-error http://127.0.0.1:5187/v1/health
curl --fail --silent --show-error https://pix.lzgames.com.br/v1/health
```

Autoteste sem cobrança real:

```bash
dotnet /opt/turborama-pix/TurboRamaPixOnlineServer.dll --self-test
```

Reinício controlado:

```bash
sudo systemctl restart turborama-pix
systemctl status turborama-pix --no-pager --full
```

Nunca reiniciar Nginx, Cloudflare Tunnel, banco ou outros serviços para resolver problema exclusivo do
TurboRama sem evidência de que eles são a causa.

## 10. Checklist pós-reinício ou pós-deploy

Exigir todos os itens:

1. `turborama-pix.service` ativo e sem ciclo de reinício;
2. PID executado como `turborama-pix:turborama-pix`;
3. listener somente em `127.0.0.1:5187`;
4. autoteste com código zero;
5. saúde local `200` e JSON com `ready:true`;
6. saúde pública HTTPS `200`;
7. cabeçalho `Strict-Transport-Security: max-age=31536000`;
8. HTTP público não retorna `200`;
9. `https://pix.lzgames.com.br/admin` retorna `404`;
10. `https://painelpix.lzgames.com.br/admin` continua protegido pelo Cloudflare Access;
11. `nginx` e `cloudflared` continuam ativos;
12. nenhuma porta empresarial ou serviço não relacionado foi alterado;
13. estado, chaves, licenças e configurações comerciais foram preservados;
14. nenhum pagamento, ativação ou licença real foi criado durante o teste.

## 11. Deploy seguro

Não implantar diretamente da árvore suja. Fluxo recomendado:

1. revisar e consolidar as mudanças locais em branch privada;
2. compilar em ambiente com SDK .NET 8; o servidor produtivo possui somente runtime;
3. produzir pacote contendo apenas os binários esperados e `CHECKSUMS-SHA256.txt`;
4. registrar tamanho e SHA-256 do ZIP e da DLL;
5. criar backup root-only datado da instalação, estado, ambiente e unidade;
6. extrair em diretório temporário e validar lista/hash antes de parar o serviço;
7. executar `--self-test` no pacote extraído;
8. parar somente `turborama-pix.service`;
9. trocar atomicamente apenas os arquivos da aplicação;
10. preservar `/etc/turborama-pix` e `/var/lib/turborama-pix`;
11. restaurar proprietário/permissões;
12. iniciar somente o TurboRama e executar o checklist completo.

Os scripts em `ops/` são referências históricas por rodada; revisar caminhos, pacote e hashes antes de
executá-los. Não assumir que um script antigo representa a DLL ativa atual.

## 12. Rollback

Rollback só deve ocorrer com backup recém-criado e alvo exato confirmado.

Procedimento conceitual:

1. registrar logs, PID, hash e saúde da versão com falha;
2. parar somente `turborama-pix.service`;
3. mover a aplicação com falha para uma pasta de quarentena datada;
4. restaurar atomicamente o conjunto completo da aplicação escolhida, nunca apenas a DLL;
5. restaurar estado/configuração somente se a falha exigir e se o par estado/chaves for coerente;
6. conferir `root:root` na aplicação e `turborama-pix:turborama-pix` no estado;
7. iniciar o serviço;
8. executar autoteste e checklist da seção 10;
9. registrar qual backup foi usado e seus hashes.

Não usar `rm -rf`, `git reset --hard` ou substituição parcial do estado.

## 13. Incidentes comuns

### Serviço não inicia

- consultar `journalctl -u turborama-pix -n 100 --no-pager`;
- confirmar existência e permissões do `server.env` e diretório de estado sem imprimir conteúdo;
- conferir runtime com `dotnet --info`;
- verificar bind de `5187` e conflito de porta;
- validar se todas as chaves obrigatórias existem e têm formato correto.

### Local funciona, público falha

- não alterar a aplicação se `/v1/health` local estiver saudável;
- verificar `cloudflared`, DNS/hostname e política Cloudflare;
- verificar certificado/borda e HSTS;
- preservar o túnel empresarial e demais ingressos.

### API funciona, painel não abre

- confirmar uso de `painelpix.lzgames.com.br`, nunca do hostname `pix`;
- verificar Cloudflare Access;
- confirmar `TURBORAMA_ADMIN_PUBLIC_HOST` sem revelar o restante do ambiente;
- conferir HTTPS encaminhado pelo proxy confiável;
- não remover Cloudflare Access para “testar”.

### Estado não carrega

- parar tentativas de escrita;
- preservar cópia do arquivo e logs;
- conferir se estado e chaves pertencem ao mesmo backup;
- não gerar chaves novas sobre o estado existente;
- restaurar primeiro em ambiente isolado.

## 14. Segurança e proibições permanentes

- repositório e pacotes devem permanecer privados;
- nunca expor `5187` em `0.0.0.0` ou diretamente à internet;
- não colocar a API `/v1/*` atrás do login humano do Cloudflare Access;
- manter o painel atrás do Cloudflare Access e do login da aplicação;
- não aceitar pendrive comum como `USB_TOKEN_BOUND`;
- não registrar chaves, senha, hash sensível, estado ou dados de clientes em handoff;
- não testar cobrança real, licença real ou transferência real durante diagnóstico;
- não alterar MariaDB, Nginx, tunnel, DNS, firewall ou outros serviços sem escopo explícito;
- usar gravação atômica e backup verificável em toda mudança produtiva.

## 15. Pendências priorizadas

1. **Crítica — release reproduzível:** identificar a fonte exata da DLL ativa, revisar, commitá-la em
   repositório privado, gerar pacote e manifesto correspondentes.
2. **Crítica — manifesto:** substituir o manifesto inconsistente somente junto de um deploy controlado
   ou após formalizar o conjunto ativo; não editar apenas para esconder a divergência.
3. **Alta — backup atual:** criar backup root-only da versão ativa, estado, ambiente e unidade, com
   manifesto externo verificável e teste de restauração isolado.
4. **Alta — árvore suja:** revisar e consolidar as mudanças locais sem descartar arquivos do usuário.
5. **Média — capacidade:** o volume raiz está em 79% (45 GiB livres); definir retenção para pacotes e
   backups, preservando pelo menos um rollback conhecido e um backup atual testado.
6. **Média — observabilidade:** adicionar alerta para serviço inativo, reinício, falha de saúde pública,
   expiração de certificado/túnel e crescimento anormal do estado.

## 16. Evidências coletadas em 2026-08-21

```text
Saúde local:
HTTP 200
{"schemaVersion":1,"ready":true,"service":"turborama-online"}

Saúde pública HTTPS:
HTTP 200
Strict-Transport-Security: max-age=31536000

HTTP público:
http://pix.lzgames.com.br/v1/health -> 400

Isolamento administrativo:
https://pix.lzgames.com.br/admin -> 404
https://painelpix.lzgames.com.br/admin -> 302 para Cloudflare Access

Autoteste:
SELF-TEST SERVIDOR ONLINE: OK

Serviço:
active, enabled, NRestarts=0
PID observado: 3142
ActiveEnterTimestamp: Thu 2026-08-20 09:14:50 -03

Recursos:
RAM: 15 GiB total, aproximadamente 12 GiB disponíveis
Swap: 11 GiB, sem uso
Disco raiz: 220 GiB, 164 GiB usados, 45 GiB livres (79%)
```

## 17. Documentos relacionados

- `README.md`;
- `STATUS-SERVIDOR-LINUX.md`;
- `HANDOFF-LINUX-SEGURANCA-TRANSPORTE-RODADA-13.md`;
- `RETORNO-LINUX-RODADA-13.md`;
- `HANDOFF-MELHORIAS-FUTURAS-SERVIDOR.md`;
- scripts históricos em `ops/`.

Este documento descreve o estado observado, não concede autorização para mudanças destrutivas ou
alterações em serviços empresariais fora do TurboRama.
