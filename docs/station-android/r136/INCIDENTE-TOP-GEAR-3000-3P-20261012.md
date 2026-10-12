# Handoff do incidente Top Gear 3000, três aparelhos

Relato em 12/10/2026: os três aparelhos ficam aguardando conexão em Top Gear 3000, independentemente de qual cria a sala. A captura USB é somente do moto g52 como convidado P2; não há captura simultânea do anfitrião e do terceiro aparelho nem recibo de produção do servidor. O catálogo R134 permite `[2,3,4]` para Top Gear 3000 e sua edição PT-BR. A sala de três usa dois links host/convidado e quatro attachments, todos da mesma epoch.

No g52, o WSS v3 respondeu a cinco PINGs em 102–122 ms, mas `nativeStatus=-1`, `localReady=false`, `readySent=-1`, tx/rx/accepted zero por mais de 40 segundos. Logo, este cliente não transmitiu a READY necessária para a barreira. Às 02:26:24 local houve close 1006 e mudança de epoch; depois apareceu 409 HTTP sem corpo registrado, cuja categoria não pode ser afirmada. A thread de inicialização RetroArch não estava presente na leitura posterior enquanto a Activity continuava em primeiro plano. A revisão de capa R136 não modifica este caminho. Hash da captura bruta privada: `6f284269a6737625d6cdc194fb7805ae4f61a515ec9df1f7aae0e7f7aa9090f7` (não anexar o log ao Git).

## O que o operador deve devolver

- Recibo de qual bundle e IDs de motor estão **efetivamente registrados** em produção, e resposta assinada fresca de `POST /v1/station/online/multiplayer/command` para o item, conteúdo, engine, core, runtime, perfil, modo e contagens exatos de Top Gear 3000. A R136 final ainda não está instalada nem registrada; o g52 roda a candidata R136, os demais R135.
- Diagnóstico member-only `barrierDiagnostics` da tentativa, se o patch R134 estiver efetivamente ativado: epoch/state, linkCount=2, expectedAttachments=4, attached/hello/paused/ready, pendingBytes e waitingReasons, sem IDs pessoais, tokens ou payload. Se não implantado, declarar isso claramente.
- Categoria e corpo saneado do HTTP 409 e causa do close WSS; correlacionar com geração/epoch e com os quatro attachments. Não atribuir o 409 a generation mismatch sem o código real.
- Evidência da porta TCP local e READY em cada telefone, sobretudo no anfitrião; o serviço não deve fabricar READY ausente nem relaxar a barreira para tirar a tela de espera.

## Capacidade por jogo

Servidor suporta até cinco só para SNES com perfil Multitap aprovado. Top Gear 3000 termina em quatro. O runtime nativo do APK herdado de R77 limita a quatro (`count>4`, máscara de quatro bits, três bindings), apesar de Java/servidor aceitarem cinco e o bsnes encaminhar P5. Super Bomberman 3 aparece na matriz histórica com `[2,3,4,5]`, mas cinco ainda não está implementado de ponta a ponta neste APK. Evitar anunciar/criar cinco até compilar, registrar e validar runtime novo com cinco aparelhos e modo específico; manter catálogo, autenticação, aprovação e limites por jogo. Cinco exigiria quatro links e oito attachments.

Este documento é diagnóstico e pedido de evidências. Não autoriza deploy, alteração de regras do hub ou publicação de dados privados.
