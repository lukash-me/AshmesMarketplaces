from typing import Optional

from pydantic import BaseModel, model_validator

class Price(BaseModel):
    basic: Optional[float] = None
    product: Optional[float] = None

class Size(BaseModel):
    price: Optional[Price] = None

class Item(BaseModel):
    id: int # Id_on_mp
    name: str # Name
    salePriceU: Optional[float] = None # Цена со скидкой
    priceU: Optional[float] = None # Цена без скидки
    wb_wallet: Optional[float] = None # Цена с wb кошельком
    brand: str # Бренд
    brandId: int # Id бренда
    sale: Optional[int] = None # Скидка
    rating: int # Округленный рейтинг товара
    volume: int # Спрос
    supplier: str = None # Имя продавца
    supplierId: int # SKU продавца
    supplierRating: Optional[float] = None # Рейтинг продавца
    totalQuantity: int # Количество остатков
    nmReviewRating: Optional[float] # Рейтинг. Не нужно. Будет рассчитываться из суммы отзывов. Нужно получать каждый отзыв.
    nmFeedbacks: Optional[int] # Количество оценок
    pics: int # Количество изображений
    image_links: Optional[str] = None # Ссылки на изображения
    root: int # Корень иерархии
    feedbacks: Optional[int] = None # Количество оценок
    sizes: Optional[list[Size]] = None # Размеры
    subjectParentId: Optional[int] = None # Категория
    subjectId: Optional[int] = None # Подкатегория
    entity: Optional[str] = None # Общее название

    @model_validator(mode="after")
    def fill_price_from_sizes(self):

        if self.sizes and len(self.sizes) > 0:
            size = self.sizes[0]

            if size.price:
                if self.priceU is None and size.price.basic is not None:
                    self.priceU = float(size.price.basic) / 100

                if self.salePriceU is None and size.price.product is not None:
                    self.salePriceU = float(size.price.product) / 100

        return self


class Items(BaseModel):
    products: list[Item]