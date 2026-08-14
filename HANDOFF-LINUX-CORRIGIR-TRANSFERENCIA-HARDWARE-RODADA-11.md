# HANDOFF LINUX — corrigir transferência de hardware — rodada 11

Data: 2026-08-14
Origem: Windows TurboRama
Destino: conversa Codex executada diretamente no servidor Linux

Este é o único handoff ativo desta rodada. Não combinar ações de handoffs anteriores. Leia o
documento inteiro antes de executar qualquer alteração.

## Resultado comprovado no Windows

O bloqueio atual não é Mercado Pago, internet ou Cloudflare. O painel real mostrou:

- licença `TR-TURBORAMA-TESTE-001` ativa;
- perfil `SOFTWARE_BOUND_ONLINE`;
- uma máquina cadastrada, offline desde 11/08/2026;
- três recusas;
- duas tentativas de 14/08/2026 registradas como `MACHINE_BINDING_MISMATCH` com
  `original_session_preserved`;
- health público `https://pix.lzgames.com.br/v1/health` em HTTP 200.

A placa-mãe foi trocada. A chave de software local permaneceu, mas o fingerprint de hardware mudou.
O servidor recusou corretamente a sessão comum. A auditoria do código encontrou o defeito: o painel
oferecia `TRANSFER_PENDING`, porém esse estado também bloqueava a emissão/conclusão do código de
ativação. Assim, não existia caminho funcional para concluir a troca autorizada.

## Correção desta rodada

O servidor passa a oferecer uma ação administrativa específica:

`Transferir para outro hardware e mostrar código único`

Depois da confirmação da senha administrativa, a ação:

1. coloca a licença em `TRANSFER_PENDING`;
2. encerra sessões e suspende vínculos anteriores;
3. emite código de uso único;
4. aceita a prova criptográfica da mesma chave em placa-mãe nova ou de chave nova após reinstalação;
5. volta a licença para `ACTIVE` somente depois da prova válida;
6. mantém exatamente uma máquina ativa e os vínculos antigos suspensos para auditoria.

O código comum de ativação continua sem poder alterar fingerprint. Nenhum bypass de licença foi
criado.

## Testes já concluídos no Windows

- compilação Release: 0 erros e 0 avisos;
- autoteste do binário publicado: código 0;
- ativação inicial e consumo único do código: aprovado;
- clone sem autorização: recusado e registrado;
- sessão concorrente: recusada;
- transferência usando a mesma chave e fingerprint novo: aprovada;
- fingerprint antigo depois da transferência: recusado;
- transferência usando chave nova: aprovada;
- dispositivo anterior após chave nova: suspenso e recusado;
- configuração, idempotência, anti-replay e cofre de credencial: aprovados;
- nenhuma cobrança real foi criada nos testes.

## Artefato autorizado

Repositório privado:

`https://github.com/luziellacerda/Servidor-pix.git`

Arquivo:

`outputs/TurboRamaPixOnlineServer-portable-RODADA11-20260814.zip`

Tamanho esperado: `91290` bytes
SHA-256 obrigatório:

`C10A84C6D913CB769C75BEA5307C2C9792F19530BE1D03EF2109787BD24D2505`

DLL publicada esperada:

`TurboRamaPixOnlineServer.dll`
Tamanho: `253440` bytes
SHA-256:

`DBE18D8DBCD7B0DC2170BE6E43BDCB5D01676C0EC5EFE66967262F38122755B5`

Não usar outro ZIP, DLL, branch ou artefato antigo. Não copiar executáveis Windows para o Linux.

## Escopo imutável

Não alterar:

- site principal;
- Cloudflare Tunnel `lz-fix`, conector ou regras já funcionando;
- Cloudflare Access;
- nginx, MariaDB, firewall, NAT ou roteador;
- portas `3302`, `3306`, `13306` e `23306`;
- hostname da API ou do painel;
- `/etc/turborama-pix/server.env`, chaves, senha administrativa ou credencial Mercado Pago;
- licença, máquina ou estado comercial durante a atualização;
- preço, pagamento, order, QR ou cobrança;
- qualquer arquivo Windows do kiosk ou EmulationStation.

A porta `5187` deve continuar somente em `127.0.0.1`.

## Fase 1 — auditoria obrigatória, somente leitura

Antes de baixar ou trocar arquivos, registrar no retorno:

1. branch e commit disponíveis no repositório remoto;
2. estado `active` e `enabled` de `turborama-pix`, `nginx`, `cloudflared` e `mariadb`;
3. definição efetiva da unit `turborama-pix`, usuário, grupo, diretório de trabalho e comando;
4. hash e tamanho dos arquivos atuais em `/opt/turborama-pix`;
5. HTTP local de `127.0.0.1:5187/v1/health`;
6. HTTP público de `https://pix.lzgames.com.br/v1/health`;
7. HTTP do site principal;
8. painel anônimo ainda interceptado pelo Cloudflare Access;
9. hostname público da API continuando a responder 404 para `/admin`, `/admin/` e `/admin/login`;
10. listeners atuais, confirmando que `5187` não está exposta;
11. contagens de clientes, licenças, máquinas, pagamentos, credenciais e preços sem mostrar dados
    secretos;
12. status da licença `TR-TURBORAMA-TESTE-001`, da máquina e das sessões.

Pare sem alterar se qualquer item divergir do estado saudável confirmado na rodada 10.

## Fase 2 — baixar e validar sem parar o serviço

1. Atualizar o clone privado existente sem apagar arquivos locais.
2. Confirmar que o commit obtido contém este handoff e as alterações de
   `AdminPanel.cs`, `ServerCore.cs` e `OnlineServerSelfTest.cs`.
3. Copiar o ZIP autorizado para uma pasta temporária nova e privada.
4. Conferir tamanho e SHA-256 do ZIP antes de extrair.
5. Extrair em diretório temporário novo, nunca diretamente em `/opt/turborama-pix`.
6. Conferir `CHECKSUMS-SHA256.txt` e o hash da DLL.
7. Recusar qualquer `.cs`, `.ps1`, `.pdb`, `.pfx`, `.key` ou arquivo com nome de segredo/token no
   pacote publicado.
8. Executar `TurboRamaPixOnlineServer.dll --self-test` a partir da pasta temporária, sob o mesmo
   usuário do serviço. O resultado obrigatório contém `SELF-TEST SERVIDOR ONLINE: OK` e código 0.

Se qualquer hash, forma do pacote ou autoteste divergir, parar sem reiniciar o serviço.

## Fase 3 — backup verificável

Antes de parar o serviço:

1. criar pasta de backup nova, com data/hora, fora de `/opt/turborama-pix`;
2. copiar integralmente a aplicação atual de `/opt/turborama-pix`;
3. localizar o arquivo de estado pela configuração já instalada sem imprimir variáveis privadas;
4. copiar o estado, seus arquivos auxiliares e metadados/permissões para o backup;
5. gerar manifesto SHA-256 do backup;
6. provar que os arquivos copiados coincidem com os originais;
7. registrar caminho, proprietário, grupo e permissões do backup, sem conteúdo secreto.

Não editar nem substituir `server.env`. Não imprimir seu conteúdo.

## Fase 4 — atualização mínima e atômica

Somente após as três fases anteriores aprovadas:

1. parar apenas `turborama-pix`;
2. confirmar que o processo antigo terminou;
3. substituir somente os arquivos publicados da aplicação pelo conteúdo validado do ZIP;
4. preservar proprietário, grupo, permissões, unit, ambiente e diretório de estado;
5. não copiar `CHECKSUMS-SHA256.txt` se a política atual de `/opt` não o utiliza;
6. iniciar apenas `turborama-pix`;
7. aguardar estado `active` dentro de um limite curto e objetivo;
8. se não ficar ativo ou entrar em reinício contínuo, executar rollback imediato.

Não executar migração manual do estado. Esta versão mantém o formato existente.

## Fase 5 — aceite depois da atualização

Confirmar:

1. serviço `turborama-pix` ativo e habilitado;
2. processo executando a DLL de hash esperado;
3. health local HTTP 200;
4. health público HTTP 200;
5. site principal HTTP 200;
6. nginx, cloudflared e MariaDB inalterados e ativos;
7. porta 5187 apenas em loopback e portas protegidas inalteradas;
8. API pública continua sem painel em `/admin*`;
9. Cloudflare Access continua protegendo o hostname administrativo;
10. após login humano, o painel abre e mostra o novo botão de transferência;
11. licença, máquina, sessão, PIX, preços e contagens comerciais permanecem iguais às de antes;
12. não houve nova recusa, código, ativação, cobrança, order ou QR durante a atualização.

Não clicar no botão de transferência nesta rodada. A ação comercial será feita no Windows somente
depois de o retorno comprovar que o servidor atualizado está estável.

## Rollback obrigatório

Se serviço, health, painel, site, isolamento, hashes ou contagens falharem:

1. parar somente `turborama-pix`;
2. restaurar a aplicação integral do backup;
3. restaurar o estado somente se seu hash tiver mudado durante a tentativa;
4. restaurar proprietário, grupo e permissões originais;
5. iniciar somente `turborama-pix`;
6. repetir health local/público, site e isolamento do painel;
7. não apagar o pacote com falha nem o backup; registrar hashes para auditoria.

Não alterar Cloudflare, nginx, MariaDB, firewall ou portas para tentar mascarar falha da aplicação.

## Critérios de parada

Parar e não improvisar se ocorrer qualquer um destes casos:

- ZIP/hash ausente ou divergente;
- repositório remoto não contém esta rodada;
- Git local possui mudança que se sobrepõe à aplicação;
- backup não pode ser comprovado;
- unit, usuário ou caminhos diferem do inventário confirmado;
- autoteste não retorna 0;
- estado não pode ser lido sem expor segredo;
- health/site/painel já estão divergentes antes da troca;
- seria necessário alterar túnel, nginx, banco, firewall ou porta;
- qualquer cobrança ou estado comercial muda sem autorização.

## Retorno obrigatório

Criar:

`/home/lz-servidor/turborama-download/Servidor-pix/RETORNO-LINUX-RODADA-11.md`

O retorno deve conter:

- data/hora inicial e final;
- commit implantado;
- hash/tamanho do ZIP e da DLL;
- caminhos dos backups e verificação dos manifestos;
- status dos serviços antes/depois;
- health local/público e site antes/depois;
- isolamento `/admin*` na API e proteção Access do painel;
- listeners antes/depois;
- contagens comerciais antes/depois;
- confirmação de que o novo botão de transferência aparece;
- `TRANSFERENCIA_EXECUTADA: NAO`;
- `CODIGO_ATIVACAO_GERADO: NAO`;
- `LICENCA_ALTERADA: NAO`;
- `MAQUINA_ALTERADA: NAO`;
- `CREDENCIAL_MERCADOPAGO_ALTERADA: NAO`;
- `COBRANCA_CRIADA: NAO`;
- resultado do autoteste;
- rollback `NAO_APLICAVEL`, `EXECUTADO_COM_SUCESSO` ou `FALHOU`;
- último passo concluído e qualquer divergência.

Nunca incluir senha, cookie, token, código de ativação, conteúdo de `server.env`, chave do estado,
Access Token, Client Secret ou cabeçalho de autorização no retorno.

## Resultado esperado

`SERVIDOR_RODADA_11_ATUALIZADO_E_ESTAVEL: SIM`

Depois desse retorno, o Windows poderá preparar a transferência pelo painel, receber um código único
e fornecê-lo somente ao configurador local para concluir o vínculo da placa-mãe atual.
