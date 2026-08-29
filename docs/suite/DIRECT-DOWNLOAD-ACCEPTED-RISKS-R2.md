# Riscos aceitos — download direto R2

Data: 2026-08-29

O proprietário solicitou e aceitou explicitamente a arquitetura de download direto:

- o servidor guarda somente o localizador permanente cifrado e metadados operacionais;
- depois da autorização de licença, dispositivo, sessão e entitlement, o aplicativo recebe o localizador em memória e baixa diretamente da hospedagem;
- o servidor não atua como proxy, relay, cache, espelho ou armazenamento do arquivo;
- quem observar o localizador no computador cliente poderá capturá-lo e reutilizá-lo enquanto ele continuar válido na hospedagem;
- não existe garantia criptográfica de integridade do arquivo baseada em SHA-256 esperado;
- `Content-Length`, `ETag`, `Last-Modified` e `Content-Range` são propriedades da resposta corrente da origem e servem apenas para limitar e tornar coerente aquela transferência/retomada;
- uma alteração legítima ou maliciosa no conteúdo mantido no mesmo localizador não poderá ser detectada por comparação com um digest oficial.

Controles que permanecem obrigatórios:

- autorização v1 vinculada a produto, licença, dispositivo, sessão, ação, item, artefato, versão, descritor, offset, validadores, expiração e grant de uso único;
- HTTPS em porta 443, origem permitida e redirects adicionais bloqueados;
- bearer/cookies da Suite nunca enviados à hospedagem;
- arquivo parcial, validação do comprimento da resposta e publicação local atômica;
- monitor de DNS, TLS, HTTPS, status, redirect, faixa pequena e validadores sem baixar o arquivo completo;
- auditoria sem registrar o localizador permanente.

Este aceite não substitui os gates de assinatura Authenticode, canário real no Windows e compra final controlada. Até esses gates passarem, o estado comercial continua **NÃO LIBERADO**.
