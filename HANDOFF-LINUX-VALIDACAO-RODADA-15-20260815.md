# HANDOFF Linux - validação final da arquitetura - rodada 15

Data da validação: 15/08/2026

Branch obrigatória: `SERVIDOR-AUTORIDADE-PIX-20260815`

Este documento corrige a arquitetura descrita no handoff da rodada 14. Não
apagar os handoffs anteriores; usá-los somente como histórico. Quando houver
conflito, esta rodada prevalece.

## Arquitetura vigente

- `pix.lzgames.com.br` é a API de máquina.
- `painelpix.lzgames.com.br/admin` é o painel humano.
- O servidor autoriza licença, máquina, sessão e a criação de uma nova cobrança.
- Mercado Pago, Loja, PDV, preços, order, QR, conciliação e créditos permanecem
  no gabinete Windows.
- O servidor não precisa receber Access Token Mercado Pago no fluxo final do
  agente desta rodada.
- Nenhuma nova cobrança é criada sem prova on-line válida.
- Uma cobrança já criada pode continuar sendo conciliada localmente se o
  servidor de licença ficar temporariamente indisponível.

## Achado bloqueante no código atual

A branch ainda registra as rotas legadas:

- `POST /v1/orders`;
- `POST /v1/orders/status`;
- `POST /v1/enrollment/mercadopago`.

Também permanecem comandos e classes para armazenar credencial e criar order no
servidor. O agente Windows final não usa essas rotas, mas a simples ausência de
uso não equivale a remoção da superfície. Antes da venda, uma nova rodada Linux
deve desativar/remover essas rotas, atualizar o autoteste para a arquitetura de
licença e eliminar a credencial bancária antiga do estado privado com backup e
rollback comprovados.

## Limites do servidor

- Não alterar o site da empresa, banco, usuários ou serviços não relacionados.
- Não remover nem recriar o túnel Cloudflare `lz-fix`.
- Não alterar as portas 3302, 3306, 13306 ou 23306.
- O serviço TurboRama continua somente em `127.0.0.1:5187`.
- Não abrir 5187 no roteador ou firewall público.
- Não incluir senha, cookie, token, código de ativação ou chave privada no Git,
  nos comandos ou no retorno.

## Estado externo comprovado no Windows

- `GET https://pix.lzgames.com.br/v1/health`: HTTP 200, JSON,
  `{"schemaVersion":1,"ready":true,"service":"turborama-online"}`.
- HSTS presente no hostname da API.
- `HEAD https://pix.lzgames.com.br/admin`: HTTP 404.
- `HEAD https://painelpix.lzgames.com.br/admin` sem cookie: HTTP 302 para
  Cloudflare Access.
- `HEAD https://lzgames.com.br/`: HTTP 200.
- Painel autenticado: uma licença ativa, uma máquina on-line e PIX liberado.
- Evento de transferência: `DEVICE_TRANSFER_PREPARED`.
- Evento de conclusão: `DEVICE_ACTIVATED`, detalhe
  `transfer:SOFTWARE_BOUND_ONLINE`.

O hostname `painelpix` também redireciona `/v1/health` para o Access. Isso não é
falha do agente: o agente usa `https://pix.lzgames.com.br/`.

## Validação do código do servidor

- Repositório e branch estavam limpos antes deste handoff.
- Build Release em diretório temporário de `H:`: 0 erros e 0 avisos.
- `TurboRamaPixOnlineServer.dll --self-test`: retorno 0.
- SHA-256 do DLL compilado no Windows:
  `83DF4874D9D3343C18AA148DF603B4D84EEA7AF4BC3411DF47158F8B14844992`.
- Busca pelos segredos reais conhecidos: zero ocorrência na árvore Git.

O autoteste usa dados sintéticos. Ele prova o protocolo, mas não prova a versão
implantada no Linux nem um pagamento real.

## Verificações obrigatórias no Linux na próxima manutenção

1. Confirmar branch e commit baixados, sem fazer merge automático.
2. Confirmar o hash do DLL realmente implantado em `/opt/turborama-pix`.
3. Executar o self-test em cópia temporária, nunca sobre a instalação ativa.
4. Confirmar o processo em `127.0.0.1:5187` e o serviço systemd ativo.
5. Confirmar `GET http://127.0.0.1:5187/v1/health` = 200 JSON.
6. Confirmar externamente `https://pix.lzgames.com.br/v1/health` = 200 JSON.
7. Confirmar que `https://pix.lzgames.com.br/admin` continua 404.
8. Confirmar que `https://painelpix.lzgames.com.br/admin` continua protegido
   pelo Access antes do login TurboRama.
9. Confirmar o site da empresa e o túnel `lz-fix` sem alteração.
10. Registrar tudo em novo `RETORNO-LINUX-RODADA-15.md`, sem segredos.
11. Auditar e remover/desativar as três rotas bancárias legadas acima.
12. Depois de backup cifrado verificável, purgar o segredo Mercado Pago antigo do
    estado Linux e provar que ativação, sessão, painel e bloqueio remoto continuam
    funcionando.

## Não executar nesta retomada

- Não cadastrar ou transportar Access Token Mercado Pago para o Linux.
- Não tratar as rotas bancárias legadas como parte da arquitetura final.
- Não gerar nova licença ou novo código de ativação: a transferência desta
  máquina já foi concluída.
- Não reiniciar site, banco ou cloudflared sem evidência específica de falha.
- Não executar comandos arbitrários pelo painel.
- Não declarar venda liberada antes do teste financeiro final no Windows.

## Pendência comercial

O agente Windows corrigido está on-line e a ativação foi aceita, porém ainda é
obrigatório gerar um QR novo com o DLL final, pagar valor controlado e confirmar
os créditos. Depois disso, revogar as credenciais Mercado Pago expostas durante
o desenvolvimento e cadastrar novas credenciais fora de chat, Git e logs.
