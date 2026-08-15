# HANDOFF LINUX - SEGURANCA DE TRANSPORTE - RODADA 13

Leia este documento inteiro antes de executar qualquer acao. Nao invente configuracao ausente e nao
imprima segredos. O servidor Linux ja hospeda servicos da empresa; a atualizacao deve ser estritamente
atomica e limitada ao aplicativo TurboRama PIX.

## Objetivo

Implantar e verificar o pacote compilado R13 do servidor PIX, preservando integralmente banco, estado,
variaveis secretas, systemd, Nginx, Cloudflare Tunnel, firewall e outros servicos.

## Proibicoes

- Nao alterar portas `3302`, `3306`, `13306` ou `23306`.
- Nao alterar MariaDB, roteador, site da empresa, tunnel `lz-fix`, DNS ou politicas Cloudflare.
- Nao criar licenca, codigo unico, pagamento ou credencial durante esta rodada.
- Nao exibir `.env`, Access Token, Client Secret, senha, chave privada ou conteudo do banco.
- Nao substituir arquivo de estado ou configuracao comercial.
- Nao executar script remoto arbitrario pelo painel.
- Nao usar o Git enquanto o repositorio continuar publico. Nesse caso, receber o ZIP por canal privado.

## Identificacao exata do pacote

- Arquivo: `TurboRamaPixOnlineServer-portable-SEGURANCA-R13-20260814.zip`.
- Tamanho: `104839` bytes.
- SHA-256: `1E1B49EE28CD9AE20BBDD24E5B53A6751E105E23859B794D108DAF4FA63C8745`.
- O ZIP deve conter exatamente cinco arquivos.
- `TurboRamaPixOnlineServer.dll`: `310784` bytes.
- SHA-256 da DLL: `FDED6FBC4488254BF53542DDBDEEB17C9C2AA651D67BFFADF697E0AC7BF0F6D9`.
- O manifesto `CHECKSUMS-SHA256.txt` deve validar todos os arquivos.
- Recusar se houver fonte, `.ps1`, `.cmd`, `.bat`, `.env`, PDB, chave ou arquivo adicional.
- Fonte do pacote: commit funcional local servidor `92dba87`.

## Preflight obrigatorio e somente leitura

Antes de mudar qualquer arquivo, registrar sem segredos:

- hash e tamanho dos cinco binarios/arquivos atualmente instalados;
- status do servico systemd e horario de inicio;
- PID, usuario, grupos e endereco/porta em escuta;
- caminho real usado por `ExecStart` e diretorio de trabalho;
- permissoes e proprietario de aplicativo, estado e configuracao;
- saude local, saude HTTPS publica e resposta HTTP publica;
- estado do Nginx e `cloudflared`, sem mostrar token ou configuracao secreta;
- espaco livre, memoria e ultimas mensagens de erro do servico com segredos mascarados;
- confirmacao de que portas e servicos empresariais citados acima permanecem ocupados pelo mesmo dono.

Se o estado real divergir do handoff, parar sem modificar e registrar a divergencia.

## Backup e implantacao atomica

1. Criar backup local datado da instalacao atual e registrar seu hash.
2. Preservar separadamente estado, `.env`, unidade systemd e configuracoes do tunnel/proxy, sem copiar
   segredos para o relatorio.
3. Extrair o ZIP em diretorio novo, verificar tamanho, SHA-256, lista exata e manifesto antes de parar o
   servico.
4. Executar o self-test do binario extraido. Exigir codigo `0`.
5. Parar somente o servico TurboRama PIX.
6. Trocar atomicamente somente os cinco arquivos do aplicativo. Nao tocar estado ou configuracao.
7. Restaurar proprietario e permissoes previamente registrados.
8. Iniciar somente o servico TurboRama PIX.
9. Se qualquer validacao falhar, restaurar atomicamente o backup e verificar o servico antigo.

## Validacao posterior obrigatoria

- Self-test do binario instalado: codigo `0`.
- Servico `active (running)` e sem reinicio continuo.
- Listener somente no endereco loopback esperado; nenhuma nova porta publica.
- Saude local HTTP por `127.0.0.1`: `200`.
- Saude publica HTTPS: `200`.
- Resposta publica HTTP nao pode continuar `200`. Aceitar `400` do aplicativo ou `301/302` se a borda
  Cloudflare tiver sido configurada para redirecionar.
- Resposta HTTPS deve conter `Strict-Transport-Security: max-age=31536000`.
- `https://pix.lzgames.com.br/admin`: `404`.
- `https://painelpix.lzgames.com.br/admin`: continua protegido pelo Cloudflare Access.
- Banco, Nginx, tunnel, site da empresa e portas reservadas permanecem exatamente no estado anterior.
- Nao realizar pagamento, ativacao ou alteracao comercial nesta rodada.

## Retorno obrigatorio

Criar `RETORNO-LINUX-RODADA-13.md` contendo:

- data e hostname;
- commit/pacote e hashes verificados;
- estado anterior e posterior do servico;
- testes e codigos HTTP observados;
- confirmacao do listener loopback e HSTS;
- confirmacao de que banco, tunnel, Nginx, site e portas reservadas nao foram alterados;
- hash do backup e caminho de recuperacao;
- qualquer divergencia ou rollback;
- zero segredo.

Nao declarar a rodada aprovada se faltar um desses resultados.
