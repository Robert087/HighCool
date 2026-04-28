import { Button } from "./Button";
import { useI18n } from "../../i18n";

export interface PaginationProps {
  currentPage: number;
  totalPages: number;
  totalCount?: number;
  pageSize?: number;
  onPageChange: (page: number) => void;
}

function getPageRange(currentPage: number, totalPages: number) {
  const start = Math.max(1, currentPage - 1);
  const end = Math.min(totalPages, currentPage + 1);
  const pages: number[] = [];

  for (let page = start; page <= end; page += 1) {
    pages.push(page);
  }

  return pages;
}

export function Pagination({
  currentPage,
  onPageChange,
  pageSize,
  totalCount,
  totalPages,
}: PaginationProps) {
  const { t } = useI18n();
  const normalizedTotalPages = Math.max(totalPages, 1);
  const safeCurrentPage = Math.min(Math.max(currentPage, 1), normalizedTotalPages);
  const pages = getPageRange(safeCurrentPage, normalizedTotalPages);
  const rangeStart = totalCount && pageSize ? (safeCurrentPage - 1) * pageSize + 1 : undefined;
  const rangeEnd =
    totalCount && pageSize ? Math.min(safeCurrentPage * pageSize, totalCount) : undefined;

  return (
    <div className="hc-pagination">
      <div className="hc-pagination__summary">
        {typeof totalCount === "number" && typeof rangeStart === "number" && typeof rangeEnd === "number"
          ? t("common.showingOf", { rangeStart, rangeEnd, totalCount })
          : t("common.pageOf", { currentPage: safeCurrentPage, totalPages: normalizedTotalPages })}
      </div>

      <div className="hc-pagination__controls">
        <Button
          variant="secondary"
          size="sm"
          disabled={safeCurrentPage <= 1}
          onClick={() => onPageChange(safeCurrentPage - 1)}
        >
          {t("common.previous")}
        </Button>

        <div className="hc-pagination__pages">
          {pages.map((page) => (
            <Button
              key={page}
              variant={page === safeCurrentPage ? "primary" : "ghost"}
              size="sm"
              onClick={() => onPageChange(page)}
            >
              {page}
            </Button>
          ))}
        </div>

        <Button
          variant="secondary"
          size="sm"
          disabled={safeCurrentPage >= normalizedTotalPages}
          onClick={() => onPageChange(safeCurrentPage + 1)}
        >
          {t("common.next")}
        </Button>
      </div>
    </div>
  );
}
