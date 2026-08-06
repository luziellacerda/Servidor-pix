# Segurança

## Dados que nunca entram neste repositório

- Access Token, Public Key, Client ID ou Client Secret do Mercado Pago;
- chaves `TURBORAMA_SERVER_STATE_KEY` e `TURBORAMA_SERVER_SECRET_KEY`;
- arquivo de estado, banco, backup, log ou cadastro de cliente;
- certificados com chave privada, tokens USB ou material do TPM;
- pacotes compilados de produção.

Se algum desses dados for publicado, não basta apagar o arquivo: revogue ou substitua a credencial, remova-a do histórico e examine acessos suspeitos.

## Falha fechada

Sem estado íntegro, chaves válidas, licença ativa, prova da máquina e sessão autorizada, o servidor não deve criar uma cobrança. A mensagem pública permanece genérica e o motivo detalhado fica somente no registro privado do servidor.

## Divulgação responsável

Não abra uma issue pública contendo credenciais, dados de clientes, detalhes de exploração ou arquivos de estado. Use um canal privado definido pelo proprietário do produto.
