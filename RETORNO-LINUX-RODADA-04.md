# Retorno Linux — atualização e configuração PIX — rodada 04

Data/hora: 2026-08-07T10:40:21-03:00  
Resultado final: `ATUALIZADO_AGUARDANDO_DADOS`

## Resumo

O clone privado foi atualizado por fast-forward e o pacote exato da rodada 04 foi localizado,
verificado e instalado. O backup anterior à troca foi criado e verificado integralmente. A troca da
aplicação foi atômica, o serviço iniciou com a versão nova e não foi necessário executar rollback.

Produção não foi cadastrada porque ainda não foram confirmados os cinco preços, o `external_id`
alfanumérico do caixa e a existência de um Access Token novo nunca exposto. Nenhuma credencial foi
solicitada ou lida.

## Pacote e versão instalada

- pacote:
  `outputs/TurboRamaPixOnlineServer-portable-RODADA04-20260807.zip`;
- tamanho: 59753 bytes;
- SHA-256 do pacote:
  `49bade5ee22a29d01b254d16141972249f0cee4c6c33eaf9e06624c885f7d63c`;
- SHA-256 do DLL instalado:
  `cad619ed6c04520af4fa5c90afa31adafcc958f874495da039d8cb38af962ed9`;
- commit atualizado: `3438b1d`;
- checksums internos: todos aprovados;
- autoteste temporário: aprovado;
- autoteste no diretório preparado: aprovado;
- validação segura simulada do caixa Mercado Pago: presente no autoteste e aprovada;
- o autoteste não acessou o Mercado Pago e não criou cobrança.

## Backup e rollback

- backup:
  `/var/backups/turborama-pix/round04-20260807T133935Z`;
- proprietário/permissão do diretório: `root:root`, modo `0700`;
- conteúdo protegido: aplicação anterior, estado, configuração privada e unidade systemd;
- manifesto: `SHA256SUMS`;
- verificação independente: todos os arquivos `OK`;
- versão anterior preservada em:
  `/opt/turborama-pix.rollback-20260807T133935Z`;
- rollback executado: NÃO;
- diretório de versão anterior não removido.

Rollback preparado, mas não executado:

1. parar somente `turborama-pix`;
2. mover a versão nova para um diretório de falha;
3. restaurar o diretório preservado para `/opt/turborama-pix`;
4. iniciar `turborama-pix`;
5. validar health local e público.

## Permissões e estado privado

- `/etc/turborama-pix`: `root:root`, modo `0700`;
- `server.env`: `root:root`, modo `0600`;
- `/var/lib/turborama-pix`: `turborama-pix:turborama-pix`, modo `0700`;
- `/opt/turborama-pix`: `root:root`, modo `0755`;
- o estado e as chaves existentes foram preservados;
- o conteúdo de `server.env` não foi exibido;
- nenhum wrapper administrativo foi criado porque a fase de produção não foi iniciada.

## Estado final de serviços e endpoints

| Item | Estado |
|---|---|
| `turborama-pix` | ativo e habilitado |
| `nginx` | ativo e habilitado |
| `cloudflared` | ativo e habilitado |
| `mariadb` | ativo e habilitado |
| health local, loopback porta 5187 | saudável, `ready: true` |
| `https://pix.lzgames.com.br/v1/health` | saudável, `ready: true` |

Os logs de inicialização da nova versão não apresentaram erro e confirmaram ambiente Production.

## Portas protegidas

As portas `3302`, `3306`, `13306` e `23306` permaneceram inalteradas:

- nenhuma regra de UFW, iptables, ip6tables, nftables ou NAT foi modificada;
- nenhum bind, listener, redirecionamento ou configuração do MariaDB foi modificado;
- 3306 continuou com o mesmo listener preexistente;
- nenhuma nova porta foi aberta;
- nginx, Cloudflare e aplicações LZGames não foram editados.

## Produção

- cliente planejado: `CLI-TURBORAMA-TESTE`;
- licença planejada: `TR-TURBORAMA-TESTE-001`;
- perfil planejado: `SOFTWARE_BOUND_ONLINE`;
- limite planejado: 1 máquina;
- licença cadastrada: NÃO;
- preços cadastrados:
  - 15 minutos: NÃO INFORMADO;
  - 30 minutos: NÃO INFORMADO;
  - 45 minutos: NÃO INFORMADO;
  - 60 minutos: NÃO INFORMADO;
  - 120 minutos: NÃO INFORMADO;
- `external_id` do caixa: NÃO INFORMADO;
- `CREDENCIAL_VALIDADA: NAO`;
- `CAIXA_VALIDADO: NAO`;
- `CODIGO_ATIVACAO_GERADO: NAO`;
- `COBRANCA_CRIADA: NAO`;
- order criado: NÃO;
- QR criado: NÃO;
- pagamento criado: NÃO.

## Arquivos e recursos criados ou alterados

- clone privado atualizado de `cee2127` para `3438b1d` por fast-forward;
- aplicação atualizada em `/opt/turborama-pix`;
- backup root-only criado em
  `/var/backups/turborama-pix/round04-20260807T133935Z`;
- versão anterior preservada em
  `/opt/turborama-pix.rollback-20260807T133935Z`;
- script criado:
  `ops/upgrade-turborama-pix-round04.sh`;
- retorno criado:
  `RETORNO-LINUX-RODADA-04.md`;
- estado, ambiente privado, unidade systemd, nginx, Cloudflare, MariaDB, firewall e aplicações LZGames:
  não alterados;
- nenhum commit ou push local foi realizado.

## Comandos executados sem segredos

```bash
git status --short --branch
git pull --ff-only origin main
stat e sha256sum do pacote esperado
unzip -l e unzip -t do pacote
sha256sum --check CHECKSUMS-SHA256.txt
dotnet TurboRamaPixOnlineServer.dll --self-test
systemctl is-active/is-enabled turborama-pix cloudflared nginx mariadb
curl dos endpoints local e público
ss -lnt com filtro das portas protegidas e da porta PIX
bash -n ops/upgrade-turborama-pix-round04.sh
pkexec /usr/bin/bash ops/upgrade-turborama-pix-round04.sh
sha256sum --check do manifesto do backup
stat sanitizado de proprietários e permissões
sha256sum do pacote e dos DLLs atual/anterior
journalctl sanitizado do serviço após a atualização
```

Uma primeira tentativa de extrair o ZIP no diretório temporário usou caminho relativo incorreto e
falhou antes de criar qualquer arquivo da aplicação ou alterar o servidor. A tentativa foi repetida
com caminho absoluto e passou em todas as verificações.

Após iniciar a versão nova, a primeira consulta local ocorreu antes de a porta abrir e falhou; a
repetição automática seguinte passou. Não houve queda persistente nem rollback.

## Último passo concluído

Atualização atômica para o binário da rodada 04, com backup verificado, versão anterior preservada e
revalidação independente de todos os serviços, endpoints e proteções.

## Próximo passo exato

Na conversa Linux, confirmar somente:

1. preço exato de 15 minutos;
2. preço exato de 30 minutos;
3. preço exato de 45 minutos;
4. preço exato de 60 minutos;
5. preço exato de 120 minutos;
6. `external_id` alfanumérico do caixa Mercado Pago, com menos de 40 caracteres;
7. que existe um Access Token novo que nunca apareceu em chat ou captura.

O Access Token não deve ser enviado no chat. Depois desses dados, retomar a fase C/D: criar o wrapper
root-only, conferir/criar a licença, cadastrar preços, inserir o token somente na entrada oculta do
terminal, validar credencial e caixa sem cobrança, gerar o código de uso único privadamente e iniciar
o teste posterior no único gabinete Windows autorizado.
