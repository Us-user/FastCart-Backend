# FastCart API — Полный справочник запросов (на русском)

Бэкенд интернет-магазина: каталог, корзина, заказы, купоны, отзывы, контент, админка.

- **Боевой адрес:** `https://fastcart-backend.onrender.com`
- **Локально:** `http://localhost:5046` (или порт, на котором запущен `dotnet run`)
- **Swagger (интерактивная документация):** `<адрес>/swagger`
- **Все эндпоинты** — с префиксом `/api/v1/...` (кроме `/health`).

> ⚠️ **Free-тариф Render «засыпает»** после ~15 мин простоя — первый запрос может идти 15–50 сек, дальше быстро.

---

## 1. Формат ответа (одинаковый у всех эндпоинтов)

Каждый ответ обёрнут в конверт:

```json
{
  "success": true,
  "message": "Текст или null",
  "data": { /* полезные данные, или null */ },
  "errors": { /* ошибки валидации по полям, или null */ }
}
```

Списки приходят постранично:
```json
{
  "success": true,
  "data": {
    "items": [ /* ... */ ],
    "pageNumber": 1,
    "pageSize": 20,
    "totalCount": 137,
    "totalPages": 7
  }
}
```
Параметры пагинации для списков: `?pageNumber=1&pageSize=20` (макс. размер страницы — 100).

---

## 2. Авторизация и роли

Используются **JWT access-токены** (короткоживущие) + **refresh-токены** (для продления сессии).

**Как пользоваться:**
1. `POST /auth/register` или `POST /auth/login` → в ответ приходят `accessToken` и `refreshToken`.
2. Защищённые запросы шлёшь с заголовком: `Authorization: Bearer <accessToken>`.
3. Когда access-токен протух — `POST /auth/refresh` с `refreshToken` → получаешь новую пару.
4. `POST /auth/logout` — отзывает refresh-токен.

**В Swagger:** нажми кнопку **Authorize** 🔒 и вставь `accessToken` (без слова `Bearer`).

**Уровни доступа (в таблицах ниже):**
- 🟢 **Публичный** — токен не нужен.
- 🔵 **Вход** — нужен любой авторизованный пользователь (покупатель).
- 🔴 **Админ** — нужна роль `Admin`.

> 🔒 **Лимит на авторизацию:** не более **10 запросов за 60 секунд с одного IP** на эндпоинты `/auth/*`. При превышении — `429 Too Many Requests`.

---

## 3. Аутентификация и аккаунт — `/api/v1/auth`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| POST | `/auth/register` | 🟢 | Регистрация. Тело: `userName`, `email`, `phoneNumber`, `password`, `confirmPassword`. Возвращает токены. |
| POST | `/auth/login` | 🟢 | Вход по email **или** телефону **или** имени. Тело: `login`, `password`. Возвращает токены. |
| POST | `/auth/refresh` | 🟢 | Обновить пару токенов. Тело: `refreshToken`. |
| POST | `/auth/logout` | 🔵 | Выход — отзывает refresh-токен. Тело: `refreshToken`. |
| POST | `/auth/forgot-password` | 🟢 | Запрос сброса пароля. Тело: `email`. Всегда отвечает 200 (защита от перебора). |
| POST | `/auth/reset-password` | 🟢 | Сброс пароля по токену из письма. Тело: `email`, `token`, `newPassword`. |
| POST | `/auth/change-password` | 🔵 | Смена пароля. Тело: `currentPassword`, `newPassword`. |
| GET | `/auth/me` | 🔵 | Текущий пользователь (id, email, телефон, роли, профиль). |

**Правила пароля:** минимум 8 символов, минимум одна заглавная, одна строчная и одна цифра.

Пример входа:
```json
POST /api/v1/auth/login
{ "login": "admin@gmail.com", "password": "Abuumar5" }
```

---

## 4. Профиль — `/api/v1/profile`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/profile` | 🔵 | Получить профиль. |
| PUT | `/profile` | 🔵 | Обновить профиль. **multipart/form-data**: поля `firstName`, `lastName`, `dob` + файл `image` (аватар). |

---

## 5. Адреса — `/api/v1/addresses`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/addresses` | 🔵 | Список адресов пользователя. |
| POST | `/addresses` | 🔵 | Добавить адрес. |
| PUT | `/addresses/{id}` | 🔵 | Изменить адрес. |
| DELETE | `/addresses/{id}` | 🔵 | Удалить адрес. |
| PUT | `/addresses/{id}/default` | 🔵 | Сделать адрес адресом по умолчанию. |

---

## 6. Каталог — справочники (категории, бренды и т.д.)

Чтение — публичное; создание/изменение/удаление — только админ. Структура одинаковая для пяти разделов.

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/categories` · `/subcategories` · `/brands` · `/colors` · `/tags` | 🟢 | Список. |
| GET | `/.../{id}` | 🟢 | Один элемент. |
| POST | `/.../` | 🔴 | Создать. |
| PUT | `/.../{id}` | 🔴 | Изменить. |
| DELETE | `/.../{id}` | 🔴 | Удалить. |

Особенности:
- **`/categories`** POST/PUT — **multipart/form-data** (есть картинка категории, поле `image`).
- **`/colors`** — у цвета есть `hex` (например `#FF0000`).
- **`/subcategories`** — привязка к категории через `categoryId`.

---

## 7. Товары — `/api/v1/products`

Главная сущность. У товара есть **опции** (например «Размер», «Цвет»), их **значения**, и **варианты** (конкретные сочетания со своей ценой/складом/SKU).

### Публичные (🟢)
| Метод | Путь | Назначение |
|------|------|-----------|
| GET | `/products` | Список с фильтрами/сортировкой/пагинацией (см. ниже). |
| GET | `/products/{id}` | Полная карточка: изображения, опции, значения, список вариантов, рейтинг. |
| GET | `/products/{id}/related` | Похожие товары. |

**Параметры фильтрации `/products`** (всё опционально):
`?search=` (поиск по названию) · `categoryId=` · `subCategoryId=` · `brandId=` · `colorId=` · `tagId=` · `minPrice=` · `maxPrice=` · `inStock=true` · `sort=` (`newest`/`price_asc`/`price_desc`/`rating`) · `pageNumber=` · `pageSize=`.

### Админские (🔴)
| Метод | Путь | Назначение |
|------|------|-----------|
| POST | `/products` | Создать товар **сразу с опциями, значениями, вариантами и картинками** (multipart; поля `options`/`variants` — JSON-строкой, `images` — файлы). |
| PUT | `/products/{id}` | Обновить товар. |
| DELETE | `/products/{id}` | Удалить товар. |
| POST | `/products/bulk-delete` | Массовое удаление. Тело: `ids: [..]`. |
| POST | `/products/{id}/images` | Добавить изображения (multipart, `images`). |
| DELETE | `/products/{id}/images/{imageId}` | Удалить изображение. |
| GET | `/products/{id}/variants` | Список вариантов товара. |
| POST | `/products/{id}/variants` | Добавить вариант. |
| PUT | `/products/{id}/variants/{variantId}` | Изменить вариант. |
| DELETE | `/products/{id}/variants/{variantId}` | Удалить вариант. |
| PUT | `/products/{id}/variants/{variantId}/stock` | Быстро изменить остаток на складе. |
| POST | `/products/{id}/options` | Добавить опцию (ось). |
| PUT | `/products/{id}/options/{optionId}` | Изменить опцию. |
| DELETE | `/products/{id}/options/{optionId}` | Удалить опцию. |

---

## 8. Отзывы

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/products/{productId}/reviews` | 🟢 | Список отзывов + сводка рейтинга (постранично). |
| POST | `/products/{productId}/reviews` | 🔵 | Оставить отзыв. Тело: `rating` (1–5), `comment`. |
| DELETE | `/reviews/{id}` | 🔵 | Удалить отзыв (свой — владелец; любой — админ). |

---

## 9. Корзина — `/api/v1/cart`

Корзина привязана к авторизованному пользователю; товары добавляются по **варианту** (`variantId`).

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/cart` | 🔵 | Текущая корзина с подсчитанными итогами. |
| POST | `/cart/items` | 🔵 | Добавить позицию. Тело: `productVariantId`, `quantity`. |
| PUT | `/cart/items/{variantId}` | 🔵 | Задать количество. Тело: `quantity`. |
| POST | `/cart/items/{variantId}/increment` | 🔵 | +1 к количеству. |
| POST | `/cart/items/{variantId}/decrement` | 🔵 | −1 к количеству. |
| DELETE | `/cart/items/{variantId}` | 🔵 | Удалить позицию. |
| DELETE | `/cart` | 🔵 | Очистить корзину. |

---

## 10. Вишлист (избранное) — `/api/v1/wishlist`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/wishlist` | 🔵 | Список избранного. |
| POST | `/wishlist/{productId}` | 🔵 | Добавить товар. |
| DELETE | `/wishlist/{productId}` | 🔵 | Убрать товар. |
| POST | `/wishlist/move-all-to-cart` | 🔵 | Перенести всё избранное в корзину. |

---

## 11. Купоны — `/api/v1/coupons` и `/api/v1/admin/coupons`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| POST | `/coupons/validate` | 🔵 | Проверить купон. Тело: `code`, `cartTotal`. Возвращает: валиден ли, размер скидки, итог. |
| GET | `/admin/coupons` | 🔴 | Список купонов (постранично). |
| GET | `/admin/coupons/{id}` | 🔴 | Один купон. |
| POST | `/admin/coupons` | 🔴 | Создать купон. |
| PUT | `/admin/coupons/{id}` | 🔴 | Изменить купон. |
| DELETE | `/admin/coupons/{id}` | 🔴 | Удалить купон. |

Поля купона: `code`, `discountType` (`Percentage`/`FixedAmount`), `discountValue`, `minOrderAmount`, `maxDiscountAmount`, `startsAt`, `expiresAt`, `usageLimit`, `perUserLimit`, `isActive`.

---

## 12. Заказы покупателя — `/api/v1/orders` и `/api/v1/returns`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| POST | `/orders/checkout` | 🔵 | Оформить заказ из корзины (одна транзакция: проверка остатков, пересчёт цен, купон, списание склада, очистка корзины). |
| GET | `/orders` | 🔵 | Мои заказы. Фильтр `?status=` (например `Cancelled`). |
| GET | `/orders/{id}` | 🔵 | Детали заказа. |
| POST | `/orders/{id}/cancel` | 🔵 | Отменить заказ (склад возвращается). |
| POST | `/orders/{id}/return` | 🔵 | Запросить возврат. Тело: `reason`. |
| POST | `/orders/{id}/pay` | 🔵 | Оплатить заказ (через выбранный способ). |
| GET | `/returns` | 🔵 | Мои запросы на возврат. |

**Тело `checkout`** (основное): адрес доставки (`shippingAddress`), способ оплаты `paymentMethod` (`CashOnDelivery`/`Bank`), опционально `couponCode`, `customerNote`.

**Статусы заказа:** `New → Ready → Shipped → Received`, плюс `Cancelled` и `Returned`.
**Статусы оплаты:** `Pending`, `Paid`, `Failed`, `Refunded`.

---

## 13. Админ: заказы и возвраты

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/admin/orders` | 🔴 | Все заказы. Фильтры: `status`, `paymentStatus`, `q` (поиск), `from`, `to`, `sort`, пагинация. |
| GET | `/admin/orders/{id}` | 🔴 | Детали заказа. |
| POST | `/admin/orders` | 🔴 | Создать офлайн-заказ вручную («Add order»). |
| PUT | `/admin/orders/{id}/status` | 🔴 | Сменить статус (с проверкой допустимых переходов). Тело: `status`, `reason`. |
| PUT | `/admin/orders/{id}/payment-status` | 🔴 | Сменить статус оплаты. Тело: `paymentStatus`. |
| GET | `/admin/returns` | 🔴 | Список возвратов. Фильтр `?status=`. |
| PUT | `/admin/returns/{id}` | 🔴 | Решение по возврату: `Approved` / `Rejected` / `Completed` (при Completed — возврат склада + рефанд). |

---

## 14. Админ: дашборд и аналитика — `/api/v1/admin/dashboard`

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/admin/dashboard/summary` | 🔴 | Продажи / Себестоимость / Прибыль. Опц. диапазон `?from=&to=`. |
| GET | `/admin/dashboard/revenue` | 🔴 | Выручка и кол-во заказов по месяцам. `?year=` (по умолчанию текущий). |
| GET | `/admin/dashboard/top-products` | 🔴 | Топ товаров. `?metric=sales\|units&take=`. |
| GET | `/admin/dashboard/recent-transactions` | 🔴 | Последние транзакции. `?take=`. |

> Прибыль считается по снимкам цены/себестоимости в момент покупки — поэтому она не «плывёт» при последующем изменении цен. В продажи/прибыль **не** входят заказы `Cancelled` и `Returned`.

---

## 15. Контент главной страницы

### Слайдеры — `/api/v1/sliders`, `/api/v1/admin/sliders`
| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/sliders` | 🟢 | Активные слайдеры (для главной). |
| GET | `/admin/sliders` · `/admin/sliders/{id}` | 🔴 | Список / один. |
| POST | `/admin/sliders` | 🔴 | Создать (multipart, картинка обязательна). |
| PUT | `/admin/sliders/{id}` | 🔴 | Изменить. |
| DELETE | `/admin/sliders/{id}` | 🔴 | Удалить. |

### Баннеры (акции/таймеры) — `/api/v1/banners`, `/api/v1/admin/banners`
| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/banners` | 🟢 | Активные баннеры (просроченные по `endsAt` скрыты). |
| GET | `/admin/banners` · `/admin/banners/{id}` | 🔴 | Список / один. |
| POST | `/admin/banners` | 🔴 | Создать (multipart; опц. `categoryId`, `endsAt` — таймер). |
| PUT | `/admin/banners/{id}` | 🔴 | Изменить. |
| DELETE | `/admin/banners/{id}` | 🔴 | Удалить. |

---

## 16. Рассылка и обращения

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| POST | `/newsletter/subscribe` | 🟢 | Подписка на рассылку. Тело: `email` (идемпотентно). |
| GET | `/admin/newsletter` | 🔴 | Список подписчиков. |
| POST | `/contact` | 🟢 | Форма обратной связи. Тело: `name`, `email`, `subject`, `message`. |
| GET | `/admin/contact-messages` | 🔴 | Список сообщений. |

---

## 17. Админ: пользователи и роли

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/admin/users` | 🔴 | Список пользователей (фильтр + пагинация). |
| GET | `/admin/users/{id}` | 🔴 | Детали пользователя (+ профиль). |
| DELETE | `/admin/users/{id}` | 🔴 | Удалить пользователя (блокируется, если есть связанные возвраты/использования купонов → 409). |
| POST | `/admin/users/{id}/roles` | 🔴 | Назначить роль. Тело: `roleId` или `roleName`. |
| DELETE | `/admin/users/{id}/roles/{roleId}` | 🔴 | Снять роль. |
| GET | `/admin/roles` | 🔴 | Список ролей. |

---

## 18. Система

| Метод | Путь | Доступ | Назначение |
|------|------|--------|-----------|
| GET | `/health` | 🟢 | Проверка живости (для Render и пингов). Без `/api/v1`. |

---

## 19. Примеры (типовые сценарии)

**Вход и сохранение токена (JS):**
```js
const base = 'https://fastcart-backend.onrender.com';
const r = await fetch(`${base}/api/v1/auth/login`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({ login: 'admin@gmail.com', password: 'Abuumar5' })
});
const { data } = await r.json();
const token = data.accessToken;     // сохрани (например в памяти/хранилище)
```

**Запрос с токеном:**
```js
await fetch(`${base}/api/v1/admin/dashboard/summary`, {
  headers: { Authorization: `Bearer ${token}` }
});
```

**Каталог с фильтром (публично):**
```
GET https://fastcart-backend.onrender.com/api/v1/products?search=футболка&minPrice=10&maxPrice=50&sort=price_asc&pageNumber=1&pageSize=20
```

**Добавить в корзину → оформить заказ:**
```js
await fetch(`${base}/api/v1/cart/items`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
  body: JSON.stringify({ productVariantId: 1, quantity: 2 })
});
await fetch(`${base}/api/v1/orders/checkout`, {
  method: 'POST',
  headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
  body: JSON.stringify({
    paymentMethod: 'CashOnDelivery',
    shippingAddress: { firstName: 'Иван', lastName: 'Иванов', streetAddress: 'ул. Пушкина 1', city: 'Москва', phoneNumber: '+700000000', email: 'i@i.com' }
  })
});
```

---

## 20. Коды ответов

| Код | Что значит |
|-----|-----------|
| 200 | Успех. |
| 400 | Ошибка валидации тела (детали — в `errors`). |
| 401 | Не авторизован (нет/протух токен). |
| 403 | Нет прав (например, не админ). |
| 404 | Не найдено. |
| 409 | Конфликт (например, дублирующий код купона, или удаление со связями). |
| 422 | Нарушено бизнес-правило (например, пустая корзина на checkout). |
| 429 | Слишком много запросов (лимит на `/auth/*`). |
| 500 | Внутренняя ошибка сервера. |

---

> 📌 Самый полный и всегда актуальный список полей запросов/ответов — в **Swagger**: `https://fastcart-backend.onrender.com/swagger`.
