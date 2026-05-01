import os
import pyexcel as pe
from loguru import logger
from datetime import datetime

from models import Item

BASE_DIR = os.path.dirname(os.path.abspath(__file__))
RESULTS_DIR = os.path.join(BASE_DIR, "results")
os.makedirs(RESULTS_DIR, exist_ok=True)


class SaveWbData:
    def __init__(self, filename: str = "wb"):
        self.filename = filename

    @staticmethod
    def now_for_title():
        current_datetime = datetime.now()
        formatted_datetime = current_datetime.strftime("%Y-%m-%d_%H-%M-%S")
        return formatted_datetime

    def generate_filepath(self, filename):
        """Генерирует абсолютный путь к файлу в папке results"""
        timestamp = self.now_for_title()
        filename_with_timestamp = f"{filename}-{timestamp}.xlsx"
        full_path = os.path.join(RESULTS_DIR, filename_with_timestamp)
        return full_path, filename_with_timestamp

    def book_save_to_path(self, book_data):
        file_path, full_filename = self.generate_filepath(filename=self.filename)
        logger.debug(file_path)

        new_book = pe.Book(book_data)
        new_book.save_as(file_path)
        return full_filename

    def wb_save(self, products: list[Item]):
        data = [
            ["SKU_Product",
             "Name",
             "Cost",
             "CostWithDiscount",
             "CostWithWBWallet",
             "Id_brand",
             "Amount",
             "SKU_Seller",
             "Images",
             "root",
             "subjectParentId",
             "subjectId",
             "Entity",
             ],
        ]

        all_saved_sku = []  # для контроля дублей
        for product in products:
            if not product:
                continue

            if str(product.id) in all_saved_sku:
                continue

            all_saved_sku.append(str(product.id))

            data.append(
                [product.id,
                 product.name,
                 product.priceU,
                 product.salePriceU,
                 product.wb_wallet,
                 product.brandId,
                 product.totalQuantity,
                 product.supplierId,
                 product.image_links,
                 product.root,
                 product.subjectParentId,
                 product.subjectId,
                 product.entity,
                 ]
            )
        book_data = {"Sheet_1": data,
                     }
        full_filename = self.book_save_to_path(book_data=book_data)
        logger.debug(full_filename)
        return full_filename