# Cadastro e liberação Station

Na administração do site, abra **Códigos Station → Novo cliente e código**.
O formulário cadastra nome/e-mail/WhatsApp opcional ou seleciona um cliente
ativo já existente no site. Não depende de o cliente ter uma licença anterior.

Escolha e confirme uma autorização:

- Venda já paga: declaração administrativa de recebimento de R$ 99,90.
- Cortesia: R$ 0,00, autorização gratuita.
- Teste: R$ 0,00, autorização gratuita identificada como teste.

As três opções concedem o plano Station vitalício para **um aparelho**. Teste
não expira automaticamente; ao encerrar o teste, bloqueie a licença. A página
não cobra, não cria pagamento do provedor, não envia WhatsApp/e-mail e não muda
as compras dos outros produtos. A senha administrativa confirma a autorização;
o cliente utiliza somente o código do aplicativo, exibido uma vez por 30 minutos.

Para dois celulares simultâneos, selecione o cliente existente e confirme
**Liberar mais um aparelho**. Para substituir o aparelho, use o atendimento da
licença existente e **Trocar celular**. Repetir a mesma solicitação conserva
cliente e licença, e nunca recupera ou emite silenciosamente outro código.

## Implementação e recuperação

`station-registration.php` valida sessão/admin, CSRF, senha, cliente ativo,
campos, autorização e limites antes de inserir cliente/recibo. Clientes novos
entram em `users` com papel customer; não se usa o cadastro global que envia
mensagens. A senha aleatória do portal não é entregue nem usada pelo aplicativo.
Clientes já existentes são lidos do banco; o nome não é aceito do navegador.

A tabela aditiva SQLite `station_registrations` guarda solicitação, digest,
cliente, administrador, autorização, motivo e licença; nunca código ou senha.
`PREPARED → CREATED → COMPLETED` permite retomar quando houver falha entre os
bancos. O mesmo request deve conservar os mesmos campos. Cadastro parcial exibe
orientação para continuar o formulário ou abrir o atendimento da licença.

O helper privado encaminha `/management/registrations` para a instância Station
admin via Unix socket. `/station/registrations` exige token, claim
`station.licenses.manage`, CSRF verificado, step-up recente e digest do IP.
O PostgreSQL serializa por cliente e registra licença/projeção/entrega/recibo/
auditoria em uma única transação. `STATION_ADMIN_V1` distingue autorização manual
de `TURBOBOX_V1` e preserva o contrato comercial existente de R$ 99,90.

O recibo registra `grantKind`, `amountCents`, motivo e ator. Cortesia/teste liquidam
uma autorização de preço zero; seu estado PAID não representa pagamento bancário.
`STATION_ADMIN_LICENSE_CREATED` aparece no histórico. Código é emitido pelo fluxo
existente de HMAC/pepper, consumo único, gerações e validade de 30 minutos. Recibo
do código não permite recuperar o texto. Nome no perfil do app segue o contrato
existente da API R41, com até 80 caracteres.

## Implantação

`deploy-registration.py` exige fonte commitada, pacote com manifesto exato,
UI17e564a e administração03/deda92c instalada. Backups PostgreSQL e SQLite são
restaurados em cópias isoladas antes da alteração. Publica somente quatro arquivos
Station e overrides próprios de management/helper; reinicia esses dois serviços.
Não há migration PostgreSQL, troca de chave/token, API, APK, ROM, proxy ou outro
serviço. A prova utiliza cliente reservado sintético, confirma dois acessos
independentes na API pública com assinatura fixada e limpa somente seus registros.

Rollback: autenticação Linux e `deploy-registration.py --rollback CAMINHO_BACKUP`.
Confere todos os hashes antes de restaurar UI/remover os dois overrides próprios.
Recusa sucessoras. Licenças, clientes e recibos criados de verdade ficam preservados;
**não restaurar o banco inteiro sobre produção**. O backup e a prova publicada são
registrados no retorno técnico, depois da implantação.
