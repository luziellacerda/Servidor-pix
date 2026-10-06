# Cadastro completo de clientes e códigos Station — 06/10/2026

## Produção

Publicado **06/10/2026 às 15:45:54 (Maceió)**. Fonte do módulo de administração:
`fe4b6317d15394397a452cd78d47ac95d0747378`.

Página: [Códigos Station](https://turbobox.lzgames.com.br/admin/station).
Login: [Administração](https://turbobox.lzgames.com.br/admin/login).

O painel anterior17e564a apenas encontrava clientes que já tinham licença Station.
O site tinha clientes ativos, mas nenhum produto Station em sua lista de produtos;
encaminhar o operador para Vendas/Pagamentos não completava o cadastro. A atualização
acrescenta cadastro e autorização Station na própria página, com cliente novo ou
existente, sem depender de uma licença anterior.

## Para cadastrar e gerar o código

1. Abra **Códigos Station → Novo cliente e código**.
2. Escolha **Novo cliente**, informe nome e e-mail; WhatsApp é opcional. Ou escolha
   **Cliente já cadastrado** e selecione um cliente ativo do site.
3. Escolha **Venda já paga — R$99,90**, **Cortesia — R$0,00** ou **Teste — R$0,00**.
4. Confirme a declaração de pagamento ou autorização gratuita, confira o motivo e
   digite **sua senha administrativa do painel**.
5. Clique **Cadastrar e gerar código**, copie o código e use na ativação do app.

O código aparece uma vez, vale **30 minutos** e tem consumo único. A senha do painel
não é uma senha de cliente e não deve ser colocada no app. Não há cobrança ou envio
automático de WhatsApp/e-mail. O fluxo não altera compras/pagamentos dos outros
produtos. Venda manual registra a declaração administrativa de recebimento; não
cria confirmação bancária do provedor.

As três opções são **vitalícias, um aparelho por licença**. O acesso marcado como
Teste não expira automaticamente: bloqueie a licença ao encerrar o teste.

Para **dois celulares simultâneos**, selecione o cliente existente e confirme
**Liberar mais um aparelho**: uma nova licença independente será criada. Para
substituir o telefone, use **Trocar celular** no atendimento da licença existente;
o anterior perde o acesso. Não marque licença adicional para uma simples troca.

Clientes do site sem licença ficam disponíveis no formulário. A tabela principal
continua sendo a lista de licenças Station e identifica venda/cortesia/teste.
Código perdido ou vencido: abra o atendimento e confirme **Gerar código**; o
anterior fica inválido. Repetir o mesmo pedido conserva o cadastro, não mostra o
código novamente e não gira a licença silenciosamente.

## Contrato que o aplicativo deve consumir

A API do aplicativo permanece **R41/a2bb176**, domínio `https://app.lzgames.com.br`.
O cadastro pelo site cria licença Station, projeção do nome e entrega elegível.
O app continua enviando o código aos endpoints existentes de desafio/conclusão de
ativação com identidade/chave do aparelho e verificando envelopes assinados com o
pin existente. Depois abre sua sessão e lê `/v1/station/me` assinado, que retorna
`licenseId`, nome do cliente e versão do perfil. O nome vem do cadastro confiável,
via `station_customer_projection`; o contrato limita a exibição a80 caracteres.

O app não chama `/management/registrations` nem `/station/registrations`: essas
rotas são exclusivas da administração privada. Não recebe senha administrativa,
token do painel, dados de pagamento, e-mail ou WhatsApp. Catálogo/capas/downloads,
revisão14/2212 jogos e relay512/1024 não mudaram. Não é necessário recompilar APK.

## Segurança, consistência e recuperação

- Sessão de administrador ativa, CSRF, senha verificada, campos/cliente ativo,
  confirmação de autorização, motivo e limites antes do cadastro.
- Novo cliente inserido com papel customer, sem reutilizar o fluxo global de
  cadastro que envia WhatsApp. Cliente existente é lido do banco; nome e referência
  de cliente não são aceitos livremente do navegador.
- SQLite: tabela aditiva `station_registrations`, etapas PREPARED/CREATED/COMPLETED.
  Recibo guarda cadastro e licença; nunca senha/código. Pode concluir a mesma
  solicitação depois de falha entre os dois bancos.
- PostgreSQL: criação transacional com lock por cliente, recibo idempotente e
  proteção contra duas requisições concorrentes. Licença adicional exige consentimento.
- Origem própria `STATION_ADMIN_V1`; `grantKind`, valor, motivo e ator ficam no
  recibo privado. `STATION_ADMIN_LICENSE_CREATED` aparece no histórico.
- Cortesia/teste liquidam uma autorização de preço zero. PAID nessa origem não é
  pagamento bancário. Comércio originalTURBOBOX_V1/R$99,90 continua com sua validação.
- Emissão usa o fluxo HMAC/pepper/generações existente; código só entregue uma vez.
  Resposta perdida após emissão exige conferência e nova emissão explícita.

[Fonte e guia técnico](https://github.com/luziellacerda/Servidor-pix/blob/fe4b6317d15394397a452cd78d47ac95d0747378/ops/station-admin/REGISTRATION.md).
Hashes/manifesto/provas em `CADASTRO-CLIENTES-STATION-PRODUCAO-20261006.json`.

## Verificações concluídas

Banco temporário, roles reais, API R41 e DLL exata do pacote: comércio/emissão
legados, claims/CSRF/step-up, cadastro pago/gratuito, recibo/digest/replay, duplicação,
concorrência, autorização de licença adicional, ativação real e perfil assinado.
Chrome1440/390: cliente novo/existente, senha errada, confirmação obrigatória,
admin/arquivado recusados, e-mail duplicado, dois códigos independentes, formulário
sem transbordamento, código ocultado ao fechar e nenhum local/sessionStorage.
Nenhuma mensagem ou compra criada nas fixtures. Retorno isolado restaura a UI,
preserva o banco e recusa sucessora antes de qualquer alteração.

Em produção, com dados sintéticos: login/página protegida e hashes públicos,
senha errada não criou cliente, cliente novo recebeu código, repetição e duplicação
recusadas, cliente existente recebeu licença adicional autorizada. **Dois aparelhos
sintéticos ativaram duas licenças independentes na API pública com assinatura/pin
verificados; ambos mantiveram sessão e o nome correto**. Zero WhatsApp/e-mail,
zero compras, dois registros sintéticos e dependências removidos. Não é teste de
gameplay em dois telefones físicos.

## Binários, serviços e retorno

| Componente | Estado conferido |
| --- | --- |
| Administração Station | PID910766, `/opt/turborama-station-management-20261006-fe4b631/backend/TurboRamaSuiteAdminServer.dll` |
| DLL de administração | `4a1d1cbd040fc5e0621b7caa04d5bb507d4163e287b2b6d216f1d3079d802e74` |
| Helper Station | PID910776, `/opt/turborama-station-management-20261006-fe4b631/station-issue-admin.py` |
| API do app | PID875574, fontea2bb176, DLLd181bf97, sem reinício |

Quatro arquivos do site: station-admin.php, station-admin.js, station-admin.css,
station-registration.php. Assets `v=20261006-2`, HTTPS idêntico ao pacote. Somente
management/helper reiniciados. Nenhuma migration PostgreSQL. Usuários/licenças
preexistentes, tokens, pepper, pin, catálogo, PIX, Suite, gateway e outros serviços
preservados; backups PostgreSQL/SQLite restaurados em cópias isoladas.

Backup privado0700: `/mnt/DADOS/station-registration-backup-20261006-fe4b631`.
Rollback específico, com autenticação Linux:

```bash
pkexec /usr/bin/python3 ops/station-admin/deploy-registration.py --rollback \
  /mnt/DADOS/station-registration-backup-20261006-fe4b631
```

Retorno confere hashes, restaura a UI e remove somente seus dois overrides. Preserva
clientes/licenças/recibos novos reais; não restaura banco sobre produção. Não usar
os rollbacks históricos de03/10 ou da interface17e564a sobre esta sucessora.

APK R41 Samsung, fonte/runtime/snapshot/manifestos preservados. POCO, gameplay em
dupla, Pessoas/Voltar/correspondência de títulos e latência externa continuam com as
pendências do retorno R41. Esta atualização resolve cadastro e acesso no painel.
