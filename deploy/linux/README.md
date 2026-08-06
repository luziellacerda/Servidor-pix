# Implantação isolada no servidor Linux

Estes arquivos são exemplos. Eles **não devem substituir** a configuração do site ou do Cloudflare Tunnel que já está funcionando.

## Separação recomendada

- aplicação: `/opt/turborama-pix`;
- estado: `/var/lib/turborama-pix`;
- variáveis privadas: `/etc/turborama-pix/server.env` com proprietário `root` e permissão `600`;
- usuário dedicado sem login: `turborama-pix`;
- escuta somente em `127.0.0.1:5187`;
- hostname Cloudflare separado, por exemplo `pix-api.seudominio.com`.

Não use a pasta do site atual, não compartilhe o usuário do site e não abra a porta 5187 no roteador ou firewall público.

## Cloudflare Tunnel existente

Faça backup do arquivo atual antes de qualquer edição. Acrescente apenas uma nova regra **antes** da regra final de erro ou catch-all:

```yaml
ingress:
  - hostname: pix-api.seudominio.com
    service: http://127.0.0.1:5187
  # mantenha aqui todas as regras que já existem
  - service: http_status:404
```

Não crie outro túnel se o túnel atual já atende ao servidor. Adicione um novo hostname ao mesmo túnel, valide a configuração e recarregue somente depois de confirmar que o site atual continua respondendo.

## Arquivo privado de ambiente

Use `.env.example` apenas como referência. No servidor, gere duas chaves diferentes de 32 bytes e guarde-as fora do Git. Não copie os valores para conversa, issue ou terminal compartilhado.

O serviço de exemplo usa `/etc/turborama-pix/server.env`. Confirme as permissões antes de iniciar:

```text
proprietário: root
permissão: leitura e escrita somente para root (600)
```

## Ordem segura de ativação

1. Faça backup verificável do site, do túnel e das configurações atuais.
2. Instale o runtime ASP.NET Core 8 fornecido pela Microsoft.
3. Crie usuário, diretórios e permissões exclusivos do TurboRama.
4. Copie somente a saída compilada para `/opt/turborama-pix`.
5. Configure o arquivo privado de ambiente.
6. Execute o autoteste local.
7. Inicie o serviço apenas em `127.0.0.1:5187`.
8. Teste `/v1/health` localmente.
9. Adicione o hostname ao túnel existente sem remover as regras atuais.
10. Teste o site antigo e o novo endpoint antes de habilitar um gabinete.

## Importante

O serviço ainda precisa de backup restaurado em teste, monitoramento, rotação de segredos e um ensaio real controlado do fluxo Mercado Pago antes de ser usado comercialmente.
