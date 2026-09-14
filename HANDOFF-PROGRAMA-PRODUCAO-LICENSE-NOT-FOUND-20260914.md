# Handoff do programa de produção: LICENSE_NOT_FOUND em 14/09/2026

## Objetivo

Este documento registra o estado verificado do ecossistema TurboRama Suite após falhas de ativação observadas em 14/09/2026. Ele deve ser lido antes de qualquer alteração no cliente Windows, na API, no proxy, no banco de licenças ou nos segredos de produção.

O objetivo é recuperar a instalação afetada sem quebrar instalações válidas, sem criar vínculos automáticos e sem misturar mudanças do Marketplace LZ Games com o TurboRama.

## Topologia confirmada

Fluxo do programa de produção:

`TurboRamaSuite/2.0 -> https://app.lzgames.com.br/v1/suite/challenges -> Cloudflare -> Nginx -> 127.0.0.1:5190 -> turborama-suite-api`

Serviços separados no mesmo servidor:

- `turborama-suite-api`: API ASP.NET do TurboRama Suite, porta interna `5190`.
- `lzgames-api`: API Node do aplicativo LZ Games e Marketplace, porta interna `8083`.
- Alterações e reinicializações do Marketplace não substituem o processo `turborama-suite-api`.
- O cliente Windows usa a autoridade assinada e a URL de produção já distribuídas. Esses valores não devem ser trocados como tentativa de correção.

## Linha do tempo observada

Horários locais do servidor, fuso `America/Maceio`:

- Entre `11:09:39` e `11:15:08`, houve respostas HTTP 200 para desafios e sessões.
- Na janela recente amostrada, foram observadas 71 respostas HTTP 200 para desafios e 71 respostas HTTP 200 para sessões.
- Às `11:27:11`, surgiu a primeira resposta HTTP 404 para `POST /v1/suite/challenges`.
- Houve novas tentativas com HTTP 404 aproximadamente às `11:27`, `11:30` e `11:33`.
- O serviço separado `lzgames-api` reiniciou entre `11:31:12` e `11:31:14`.
- A primeira falha do TurboRama ocorreu cerca de quatro minutos antes dessa reinicialização.
- O serviço `turborama-suite-api` não foi reiniciado durante as alterações analisadas e permanecia ativo desde `10/09/2026 11:07`.
- Por volta de `11:34`, sondagens confirmaram resolução, TLS, proxy, roteamento e respostas estruturadas da API.

## Conclusão técnica

A requisição com falha alcançou a rota correta da API ASP.NET. Portanto, o evento analisado não corresponde a falha de DNS, certificado TLS, Cloudflare, Nginx, conexão recusada, timeout ou indisponibilidade geral do servidor.

No manipulador ativo de `POST /v1/suite/challenges`:

- Licença inexistente retorna HTTP 404 com código `LICENSE_NOT_FOUND`.
- Dispositivo não cadastrado ou não permitido retorna HTTP 403.
- Desafio inválido ou incompatível retorna HTTP 409.
- JSON inválido retorna HTTP 400 com código `JSON_INVALID`.

A inferência sustentada pelas evidências é que a tentativa com falha enviou um identificador de licença que não estava presente no banco de produção consultado pela API naquele momento.

O corpo original da requisição não é gravado integralmente nos logs. Portanto, este handoff não afirma conhecer o identificador completo enviado. O mesmo endereço público e o mesmo `User-Agent` também não provam que todas as tentativas vieram do mesmo computador; mais de uma instalação pode compartilhar a mesma rede.

## Estado dos serviços durante a auditoria

Os seguintes pontos responderam HTTP 200:

- `http://127.0.0.1:8083/api/health`
- `https://app.lzgames.com.br/api/health`
- `https://turbobox.lzgames.com.br/api/mobile/v1/health`

Os processos `lzgames-api`, `turborama-suite-api` e Nginx estavam ativos.

Sondagens adicionais confirmaram o contrato da rota:

- `GET /v1/suite/challenges` retorna HTTP 405, pois a rota aceita POST.
- `POST /v1/suite/challenges` com corpo vazio ou `{}` retorna HTTP 400 e `JSON_INVALID`.
- Uma requisição sintética com licença inexistente reproduz HTTP 404 e `LICENSE_NOT_FOUND`.

Essas sondagens comprovam conectividade e roteamento. Elas não devem ser usadas para cadastrar, substituir ou adivinhar licenças.

## Artefato ativo verificado

Binário ativo no momento da auditoria:

`/opt/turborama-suite-r5-releases/downloads-bd82bc3-20260908/server/TurboRamaSuiteOnlineServer.dll`

SHA-256:

`93939be3153d1ae5b465406eca9f1ddcb5c85635900ea6d60aaaeb2f44d45cb1`

O hash coincide com o DLL compilado do release correspondente disponível no servidor de trabalho. O arquivo ativo possui data de modificação de 08/09/2026.

## Limitação da auditoria

O conteúdo do PostgreSQL não foi consultado diretamente porque o acesso privilegiado ao ambiente e ao arquivo de configuração do serviço exige credencial administrativa indisponível nesta sessão. Nenhuma permissão foi contornada.

Consequências dessa limitação:

- Foi comprovado pelo comportamento da API que a licença apresentada não foi encontrada.
- Não foi comprovado diretamente se o registro foi removido, se nunca existiu, se pertence a outro ambiente ou se o cliente passou a usar outro estado local.
- Uma auditoria administrativa somente leitura no PostgreSQL ainda é necessária para distinguir essas hipóteses.

## Procedimento para o programa Windows de produção

Executar nesta ordem:

1. Identificar o computador exato que apresentou a mensagem e verificar se existe uma segunda instalação, cópia portátil, restauração de backup ou pasta antiga do programa.
2. Registrar caminho do executável, versão exibida, data do arquivo e SHA-256 do executável em execução.
3. Inspecionar o armazenamento local de licenciamento sem apagar, regenerar ou editar seus arquivos.
4. Registrar horário exato, status HTTP, campo `code` da resposta e identificador de correlação, quando disponível.
5. Comparar apenas o hash SHA-256 ou uma forma mascarada do identificador local com o registro ativo de produção, por operador autorizado.
6. Verificar se o estado local mudou depois da última sessão confirmada às `11:15:08`.
7. Confirmar se a instalação utiliza a URL oficial e a autoridade assinada já distribuída.
8. Preservar o estado local original até concluir a correlação com o banco e a trilha de auditoria.

Não publicar no GitHub, em mensagens ou em capturas:

- Identificador completo de licença.
- Chave privada.
- Token de acesso.
- Conteúdo de `server.env`.
- Credencial do PostgreSQL.
- Dados pessoais do cliente.

## Procedimento administrativo no servidor

Um operador com autorização deve fazer uma consulta somente leitura:

1. Localizar nos registros de licenças e dispositivos a identidade mascarada ou o hash fornecido pelo cliente.
2. Consultar a trilha de auditoria entre `11:15` e `11:27` em 14/09/2026.
3. Verificar expiração, revogação, transferência, vínculo de dispositivo e ambiente.
4. Confirmar se houve operação administrativa no intervalo.
5. Comparar o dispositivo da última sessão HTTP 200 com o dispositivo da primeira tentativa HTTP 404.
6. Registrar a conclusão sem copiar segredos ou identificadores completos para este repositório.

Se for necessária recuperação ou transferência, usar somente o fluxo administrativo controlado já existente, com confirmação da identidade do cliente e registro de auditoria.

## Invariantes obrigatórios

- Não editar `server.env`, chaves RSA, certificados ou configuração do Nginx para corrigir `LICENSE_NOT_FOUND`.
- Não desativar validação TLS, assinatura, pinning, desafio, vínculo de dispositivo ou expiração.
- Não cadastrar automaticamente uma licença desconhecida.
- Não mapear uma licença desconhecida para um cliente por semelhança de nome, IP ou dispositivo.
- Não copiar estado de licenciamento entre computadores sem o fluxo oficial de transferência.
- Não reiniciar ou republicar o TurboRama como primeira tentativa; a rota já estava alcançável.
- Não alterar o Marketplace LZ Games como tentativa de corrigir a licença do TurboRama.
- Não misturar código do cliente Windows, API TurboRama e aplicativo Android/iOS em um único commit.
- Não remover logs, banco, backups ou arquivos locais antes de preservar evidências.
- Não inserir segredos, licenças completas ou dados pessoais em commits.

## Critérios de aceite para encerrar o incidente

O incidente só pode ser encerrado quando todos os itens abaixo forem atendidos:

- A instalação afetada está inequivocamente identificada.
- O identificador local mascarado ou seu hash foi correlacionado por operador autorizado.
- A causa da divergência de estado foi registrada.
- O fluxo de desafio retorna HTTP 200 para a instalação correta.
- A sessão autenticada é aberta e renovada normalmente.
- Instalações válidas existentes continuam funcionando.
- Não houve relaxamento de segurança, criação automática de licença ou troca de autoridade.
- A correção, se houver, possui trilha de auditoria e plano de reversão.

## Estado de alteração

Nenhum arquivo, configuração, licença, dispositivo ou registro do TurboRama foi alterado durante o diagnóstico que originou este handoff.

Não há rollback de servidor a executar com base nesta auditoria. A próxima ação correta é correlacionar, de forma autorizada e somente leitura, o estado local da instalação afetada com o banco de produção.
