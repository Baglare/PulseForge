# PulseForge Planning Pack v0.1

PulseForge, kullanıcı tarafından seçilen bir müziğin ritmik yapısını görünür hâle getiren ve bu yapıdan oynanabilir 2D dövüş koreografisi üreten deneysel bir ritim-dövüş projesidir.

Bu paket kod üretmekten önce proje sınırlarını, mimariyi, alan modelini ve geliştirme sırasını sabitlemek için hazırlanmıştır. Belgeler kesin ve değişmez değildir; karar değiştiğinde belge de değiştirilir. Değişikliklerin bilinçli yapılması yeterlidir.

## Belge sırası

1. `START-HERE.md`: İlk Unity, Git ve Codex oturumunun uygulama sırası.
2. `docs/01-requirements.md`: Ürünün ne yapacağı ve ne yapmayacağı.
3. `docs/02-architecture.md`: Büyük parçaların nasıl ayrılacağı.
4. `docs/03-domain-model.md`: İlk sınıflar, sorumlulukları ve ilişkileri.
5. `docs/04-development-roadmap.md`: Geliştirme aşamaları ve her aşamanın bitiş ölçütü.
6. `docs/05-working-method.md`: Her Codex görevinin nasıl verileceği ve nasıl denetleneceği.
7. `prompts/01-domain-foundation.md`: İlk, dar kapsamlı Codex görevi.
8. `checklists/01-domain-foundation-review.md`: İlk Codex çıktısını değerlendirme listesi.
9. `AGENTS.md`: Kodlama aracının tüm görevlerde uyması gereken kalıcı kurallar.

## Şimdiki hedef

İlk hedef müzik analizi, sahne, animasyon veya kullanıcı arayüzü değildir. İlk hedef, sabit bir ritim olayı için oyuncu girdisini `Perfect`, `Good` veya `Miss` olarak değerlendiren saf C# çekirdeğini testlerle kurmaktır.

Bu küçük görünür. Zaten öyle olması gerekiyor. Küçük ve doğrulanmış temel, büyük ve hayalî temelden daha kullanışlıdır.
