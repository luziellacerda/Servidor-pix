# HANDOFF LINUX — painel profissional e auditoria — rodada 12

Data: 2026-08-14  
Origem: Windows TurboRama  
Destino: conversa Codex executada diretamente no servidor Linux

Este é o único handoff ativo desta rodada. Leia tudo antes de agir. Não combinar comandos, pacotes
ou suposições de rodadas anteriores.

## Objetivo único

Atualizar somente a aplicação `turborama-pix` para a versão que reúne:

- a correção de transferência de hardware preparada na rodada 11;
- o painel administrativo profissional da rodada 12;
- indicadores de licenças, máquinas, PIX autorizado e tentativas recusadas;
- pesquisa e filtros de licenças, máquinas e auditoria;
- confirmação explícita e estado ocupado nas ações críticas;
- exportação autenticada da auditoria em CSV;
- separação visual e operacional entre licenciamento on-line e pagamento local.

Não executar transferência, não gerar código, não alterar licença e não criar cobrança nesta rodada.

## Regra de arquitetura

O servidor administra licença, vínculo de máquina, sessão, status, bloqueio remoto e auditoria.
Preços, Access Token, PDV, provedor, QR Code, confirmação do pagamento e concessão de créditos
permanecem no gabinete. O painel novo não oferece formulários de preço ou Mercado Pago.

Timeout, DNS, perda de internet, túnel indisponível ou erro `5xx` não podem encerrar jogos, retirar
créditos ou desativar o funcionamento local. Somente uma recusa explícita e autenticada pode
bloquear uma nova compra PIX.

## Evidência concluída no Windows

- compilação Release: 0 avisos e 0 erros;
- compilação, cache NuGet, objetos, binários e publicação realizados em `H:\TurboRamaTemp`;
- autoteste do binário publicado: código 0;
- painel real temporário: login, cookie, CSRF e dashboard autenticado aprovados;
- cabeçalhos de segurança, CSP, `X-Frame-Options` e `no-store` aprovados;
- indicadores, pesquisa de licença, filtro de auditoria e confirmações aprovados;
- exportação CSV autenticada: HTTP 200, UTF-8 BOM, cabeçalho, download e auditoria aprovados;
- pacote sem `.cs`, `.ps1`, `.pdb`, `.pfx`, `.key`, token, credencial ou chave privada;
- nenhum dado comercial real e nenhuma cobrança usados nos testes;
- produção, Cloudflare, site, estado e serviços Linux não foram alterados no Windows.

## Artefato único autorizado

Repositório privado:

`https://github.com/luziellacerda/Servidor-pix.git`

Arquivo:

`outputs/TurboRamaPixOnlineServer-portable-RODADA12-20260814.zip`

Tamanho esperado: `104376` bytes  
SHA-256 obrigatório:

`FF0C82974AD721B6F7007CE1C56CE2A1402FF78B4A75A34EDBDC19D7CFD63C81`

DLL publicada:

`TurboRamaPixOnlineServer.dll`

Tamanho esperado: `309760` bytes  
SHA-256 obrigatório:

`4C5A51D8B547FEC90CB276737BCC44BE476434BDA9EFCDFBCAA342CC86EFA43F`

Forma esperada do pacote: cinco arquivos, sendo quatro arquivos publicados e
`CHECKSUMS-SHA256.txt`. Recusar pacote com fonte, símbolo de depuração, script, chave ou segredo.

O commit remoto usado deve conter este handoff e as alterações de `AdminPanel.cs`,
`OnlineServerSelfTest.cs`, `ServerCore.cs`, `README.md`, `deploy/linux/README.md` e
`COMPILAR-SERVIDOR-PIX-ONLINE.ps1`. Se o repositório remoto ainda não contiver tudo, parar sem
alterar o servidor.

## Escopo imutável

Não alterar:

- site principal;
- Cloudflare Tunnel `lz-fix`, conector, ingressos ou Cloudflare Access;
- nginx, MariaDB, firewall, NAT ou roteador;
- portas `3302`, `3306`, `13306` e `23306`;
- hostname da API `pix.lzgames.com.br`;
- hostname do painel `painelpix.lzgames.com.br`;
- porta local `5187`, que deve permanecer somente em `127.0.0.1`;
- unit do serviço, usuário, grupo ou diretório de trabalho;
- `/etc/turborama-pix/server.env`, senha, chaves, cookie ou diretório de estado;
- clientes, licenças, máquinas, sessões, preços, credenciais ou pagamentos;
- qualquer arquivo Windows do kiosk ou EmulationStation.

Não instalar dependência nova e não atualizar o sistema operacional nesta rodada.

## Fase 1 — auditoria obrigatória e somente leitura

Antes de baixar ou trocar arquivo, registrar no retorno:

1. branch e commit disponíveis no remoto;
2. serviço `turborama-pix` ativo e habilitado;
3. `nginx`, `cloudflared` e `mariadb` ativos e habilitados;
4. definição efetiva da unit, usuário, grupo, diretório de trabalho e comando;
5. versão do runtime .NET utilizado pelo serviço;
6. hash e tamanho dos arquivos atuais de `/opt/turborama-pix`;
7. health local em `127.0.0.1:5187/v1/health` com HTTP 200;
8. health público em `https://pix.lzgames.com.br/v1/health` com HTTP 200;
9. site principal com HTTP saudável;
10. painel anônimo interceptado pelo Cloudflare Access;
11. hostname da API respondendo 404 para `/admin`, `/admin/` e `/admin/login`;
12. listener `5187` exclusivamente em loopback;
13. contagens de clientes, licenças, máquinas, sessões, pagamentos, credenciais e preços, sem
    imprimir segredo;
14. status e último contato da licença e máquina existentes;
15. hash autenticado ou backup verificável do arquivo de estado, sem exibir seu conteúdo.

Parar sem alterar se site, túnel, painel, API, serviço, isolamento, porta ou estado já estiverem
divergentes.

## Fase 2 — baixar e validar sem interromper o serviço

1. Atualizar o clone privado existente sem apagar arquivo local.
2. Confirmar que o commit remoto contém integralmente a rodada 12.
3. Copiar o ZIP autorizado para uma pasta temporária privada e nova.
4. Conferir tamanho e SHA-256 antes de extrair.
5. Extrair em pasta temporária nova, nunca diretamente em `/opt/turborama-pix`.
6. Validar cada entrada de `CHECKSUMS-SHA256.txt`.
7. Confirmar tamanho e SHA-256 da DLL.
8. Confirmar a forma exata de cinco arquivos e ausência dos tipos proibidos.
9. Executar a DLL temporária com `--self-test` usando o mesmo usuário do serviço.
10. Exigir código 0 e mensagem iniciada por `SELF-TEST SERVIDOR ONLINE: OK`.

Qualquer divergência encerra a rodada antes de parar o serviço.

## Fase 3 — backup verificável

Antes da troca:

1. criar backup novo, datado e privado fora de `/opt/turborama-pix`;
2. copiar integralmente a aplicação atual;
3. localizar o estado pela configuração instalada sem imprimir variáveis privadas;
4. copiar estado, auxiliares e metadados preservando permissões;
5. guardar definição efetiva da unit e permissões da aplicação;
6. gerar manifesto SHA-256 do backup;
7. comparar backup e origem antes de prosseguir.

Não editar nem substituir `server.env`. Não mostrar seu conteúdo.

## Fase 4 — atualização mínima e atômica

Somente após as fases anteriores aprovadas:

1. parar apenas `turborama-pix`;
2. confirmar que o processo antigo terminou;
3. publicar somente os quatro arquivos de aplicação validados;
4. preservar proprietário, grupo e permissões atuais;
5. preservar unit, ambiente, estado, site, túnel e proxy;
6. iniciar apenas `turborama-pix`;
7. exigir estado ativo dentro de um limite curto;
8. se falhar ou reiniciar continuamente, aplicar rollback imediato.

Não fazer migração manual. Não editar JSON de estado. Não remover registros legados.

## Fase 5 — aceite sem mutação comercial

Confirmar depois da troca:

1. serviço ativo e habilitado, sem ciclo de reinício;
2. processo executando a DLL com o hash autorizado;
3. health local HTTP 200;
4. health público HTTP 200;
5. site principal saudável;
6. nginx, cloudflared e MariaDB inalterados e ativos;
7. listener `5187` somente em loopback;
8. hostname da API continua retornando 404 em `/admin*`;
9. Cloudflare Access continua protegendo o hostname administrativo;
10. login humano do painel continua exigido;
11. após login, aparecem os quatro indicadores, pesquisa, filtros, auditoria e exportação CSV;
12. não aparecem formulários de preço ou Mercado Pago;
13. status atual aparece selecionado corretamente em cada licença;
14. download CSV exige autenticação e produz arquivo com cabeçalho de auditoria;
15. contagens e hashes do estado comercial permanecem idênticos aos da fase 1;
16. nenhuma recusa, sessão, código, ativação, transferência ou pagamento foi criado pelo aceite.

Não clicar em suspender, revogar, PIX, reautenticar, transferir ou gerar código. O teste das ações é
somente visual nesta rodada.

## Rollback obrigatório

Se serviço, health, site, painel, isolamento, hashes, permissões ou estado divergirem:

1. parar somente `turborama-pix`;
2. restaurar integralmente a aplicação do backup;
3. restaurar o estado somente se seu hash tiver mudado;
4. restaurar proprietário, grupo e permissões originais;
5. iniciar somente `turborama-pix`;
6. repetir health local/público, site, painel e isolamento;
7. manter pacote com falha e backup para auditoria.

Não alterar Cloudflare, nginx, banco, firewall ou portas para mascarar falha da aplicação.

## Critérios de parada

Parar e não improvisar se:

- o remoto não contiver esta rodada;
- ZIP, DLL, tamanho ou hash divergirem;
- o pacote tiver forma ou arquivo proibido;
- o autoteste não retornar 0;
- o backup não puder ser comprovado;
- a configuração instalada diferir da auditoria;
- o serviço já estiver instável antes da troca;
- for necessário editar ambiente, estado, túnel, nginx, banco, firewall ou porta;
- qualquer operação comercial ou de pagamento for disparada;
- qualquer segredo aparecer em saída ou arquivo de retorno.

## Retorno obrigatório

Criar exatamente:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-12.md`

O retorno deve conter:

- data/hora inicial e final;
- commit implantado;
- hash e tamanho do ZIP e da DLL;
- caminho e manifesto dos backups;
- runtime e definição do serviço;
- status dos quatro serviços antes/depois;
- health local, health público e site antes/depois;
- isolamento `/admin*`, proteção Access e listener antes/depois;
- hash/contagens comerciais antes/depois;
- resultado do autoteste;
- presença dos indicadores, filtros, confirmações e exportação CSV;
- ausência de formulários de preço e Mercado Pago;
- `TRANSFERENCIA_EXECUTADA: NAO`;
- `CODIGO_ATIVACAO_GERADO: NAO`;
- `LICENCA_ALTERADA: NAO`;
- `MAQUINA_ALTERADA: NAO`;
- `SESSAO_ALTERADA: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- rollback: `NAO_APLICAVEL`, `EXECUTADO_COM_SUCESSO` ou `FALHOU`;
- último passo concluído e qualquer divergência.

Nunca incluir senha, cookie, token, código de ativação, conteúdo de `server.env`, chave, credencial,
Access Token, Client Secret ou cabeçalho de autorização.

## Resultado esperado

`SERVIDOR_RODADA_12_PAINEL_PROFISSIONAL_E_ESTAVEL: SIM`

