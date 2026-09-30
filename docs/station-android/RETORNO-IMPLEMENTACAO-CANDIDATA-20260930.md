# Retorno ao handoff Android — implementação candidata no servidor

Data: 30/09/2026. Escopo: branch isolada `feat/station-android-contract-20260930` do repositório privado `Servidor-pix`. Este retorno responde ao [handoff do cliente](https://github.com/luziellacerda/TurboElden/blob/db0a6b722a031885bc5d0355ed3390289150b325/docs/server/HANDOFF-CLIENTE-STATION-ANDROID-20260930.md). **Não é autorização nem prova de implantação.** Nenhum processo, unidade, proxy, banco ou APK de produção foi modificado.

## Decisões e linha de base

- O responsável informou preço de **R$ 99,90** e licença **sem expiração**. O candidato usa `amountCents=9990`, `currency=BRL`, `license_term=LIFETIME` e `expires_at=NULL`. O código de ativação dura 15 minutos e cada sessão dura 180 segundos; esses prazos técnicos não expiram a licença comercial.
- O código pressupõe **um aparelho ativo por licença** e transferência/reemissão manual. A confirmação explícita dessa política ainda está pendente; **não ativar o produto** antes dela.
- O SKU candidato é `STATION_ANDROID_LIFETIME_1_DEVICE`; produto e aplicação são `TURBORAMA_STATION_ANDROID`. Nenhuma licença Suite Windows vira Android por conversão automática.
- Um inventário somente de leitura confirmou os quatro serviços Turborama ativos e os endpoints de saúde locais com HTTP 200. O monitor de conteúdo já estava em estado `failed` antes desta alteração. A sessão PostgreSQL efetivamente usada pelos serviços locais está no banco `postgres`, com ledger `suite.schema_migrations` de `001` até `027`. Um outro banco de restauração contém apenas `001`–`009` e **não** foi usado para escolher a numeração. As consultas não leram compradores, pedidos, tokens nem segredos.

## Código entregue nesta branch

1. `migrations/suite/028_station_android.up.sql`: ampliação explícita das constraints de produto/SKU, tabelas separadas para aparelhos, desafios, sessões e projeção do comprador, índices, grants e ledger. É uma migration **candidata, não aplicada em produção**. Ela passou ao reexecutar `001`–`028` em PostgreSQL 16 descartável.
2. API Suite: rotas `/v1/station/activations/challenge`, `/activations/complete`, `/challenges`, `/sessions` e `/me`, com RSA-PSS/SHA-256 e Base64URL sem padding, chave de assinatura e pepper Android independentes da Suite, hash de bearer no banco, validação de licença/entrega financeira/aparelho/geração a cada consulta, desafio de uso único e ativação transacional. `Station:Enabled=false` por padrão; as rotas respondem 503 sem a flag. O protocolo Suite/ES não foi alterado.
3. Rotas `/v1/station/catalog` e `/downloads/authorize` existem **somente como bloqueios explícitos**: com sessão válida, retornam 503 e nunca entregam URL privada. É deliberado até o catálogo Android e o gateway terem uma autorização própria.
4. Administração interna via socket existente: evento de comércio Android autenticado em `/commerce/station/events`, emissão de código, sincronização versionada do nome, consulta de status, bloqueio/desbloqueio, revogação de sessão exata e transferência. `STATION_COMMERCE_ENABLED` é falso quando ausente. Ações exigem claim, CSRF confirmado e confirmação recente no contrato interno; recibos de ações são idempotentes. O código não é colocado em logs ou auditoria, apenas devolvido ao chamador interno autorizado para entrega.
5. `openapi-candidato.yaml` descreve o contrato público parcial. O servidor assina os **bytes exatos** do JSON UTF-8 retornado, sem recodificação posterior. O Android assina os bytes exatos enviados no seu envelope.

## Testes realizados sem produção

- Compilação .NET 8 da API e do backend administrativo: zero erros/avisos.
- Suite online tests existentes e novo teste de protocolo Station: passaram, inclusive vetores antigos Suite, prova RSA válida/inválida e envelope de resposta.
- PostgreSQL 16 descartável: migrations `001`–`028` executaram; teste sintético de ativação concorrente (apenas um aparelho vinculado), rejeição de replay, sessão, perfil vinculado à licença e revogação passou **usando o papel restrito `turborama-suite` na API**. Foram conferidas também as permissões essenciais do papel administrativo no banco descartável.
- Os autotestes existentes do backend administrativo passaram. O corpo de eventos internos agora é rejeitado durante a leitura caso exceda 8 KiB, inclusive quando chega sem `Content-Length`.
- Backend administrativo isolado, conectado apenas ao contêiner de teste: evento pago gerou uma única licença; repetição retornou a mesma; emissão de código retornou um código de teste; transferência repetida preservou a mesma geração; reemissão manual, suspensão financeira e atualização de nome funcionaram. Nenhum código de teste é documento de produção.
- API isolada com flag padrão desligada: `/health` 200, rota Station 503 e rota Suite 503 com Suite desativada.

## Contrato interno do comércio candidato

O site atual usa apenas o evento Suite de sete campos e o SKU Windows. O candidato Android **não** reutiliza esse evento silenciosamente. `/commerce/station/events` exige `sourceSystem=TURBOBOX_V1`, `sourceProductSku=STATION_ANDROID_LIFETIME_1_DEVICE`, `amountCents=9990`, `currency=BRL`, `sourceEventId` hex de 32 caracteres, `sourceVersion>0`, `sourcePurchaseId`, `sourceItemKey`, `eventType` (`PURCHASE_PAID` ou `PURCHASE_SUSPENDED`), `customerRef`, `displayName` e `payloadDigest`. O digest é SHA-256 hexadecimal minúsculo de onze valores unidos por LF **sem LF final**: `sourceSystem`, `sourceEventId`, `sourcePurchaseId`, `sourceItemKey`, `sourceVersion`, `sourceProductSku`, `eventType`, `amountCents`, `currency`, `customerRef`, `displayName`. O produtor deve obter `customerRef` e nome do cadastro comercial autenticado, não do APK.

Pagamento repetido não duplica licença; cancelamento mais novo não é revertido automaticamente por evento pago atrasado. O endpoint de perfil atualiza o nome somente se `profileVersion` aumentar, preservando a relação compra → titular. O painel interno nunca mostra o valor do código já emitido.

## Bloqueios para completar o handoff e ativar vendas

1. **Checkout/entrega no site.** A fonte do TurboBox foi encontrada em um worktree limpo de outro repositório, com SKU Windows e preço próprios. Não foi editada nem implantada: precisa de branch aditiva no repositório `TurboBox`, produto Android configurado em R$ 99,90, evento Android novo e ligação confiável com o cadastro/área do cliente. A existência da rota interna não cria uma venda no site.
2. **Painel visual existente.** As operações internas estão compiladas, mas o BFF e as páginas `/admin`/`/admin/clientes/{licenseId}` ainda não chamam as rotas Station. Não há botão ou cartão Android funcionando no painel em produção. A vinculação `customerRef` ↔ cadastro da página deve ser confirmada, não deduzida por nome.
3. **Catálogo privado e gateway.** Falta a fonte controlada dos cerca de 18 mil itens Android, seus IDs/plataformas e um adapter de concessão do gateway que não finja sessão Suite. Sem isso, catálogo e download ficam fechados. Nunca enviar URLs privadas ao Git ou usar redirect público como controle de acesso.
4. **Cliente e homologação.** O APK candidato do handoff continua com `StationConfig.ENABLED=false`, não instalado, e `GuiStore.cpp`/ponte nativa de download não estão disponíveis aqui. Nenhuma URL HTTPS, chave pública/keyId comercial ou licença sintética de ambiente homologado foi emitida. O protocolo precisa ser revisado em Android real antes de ligar a flag.
5. **Operação.** Revisão de segurança, teste de permissões com os papéis reais, backup restaurável, snapshot da página/site, regressões PIX/Suite/ES/conteúdo, teste de compra sintética ponta a ponta, release imutável e rollback ainda são exigidos. A migration `028` não foi aplicada em produção e os binários ativos não foram trocados.

## Próximo trabalho seguro

Confirmar o limite de aparelhos e fornecer as fontes privadas de catálogo/ponte nativa. Integrar o TurboBox e o painel em branches próprias, sem editar checkout ou worktree de produção. Testar o contrato `openapi-candidato.yaml` contra o APK candidato, inclusive vetores binários compartilhados .NET/Java, e preparar staging HTTPS isolado. Só após tudo isso avaliar implantação com a flag desativada inicialmente e revisão de cada serviço afetado.
