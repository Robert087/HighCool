import { Button } from "./Button";

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
  if (totalPages <= 1) {
    return null;
  }

  const pages = getPageRange(currentPage, totalPages);
  const rangeStart = totalCount && pageSize ? (currentPage - 1) * pageSize + 1 : undefined;
  const rangeEnd =
    totalCount && pageSize ? Math.min(currentPage * pageSize, totalCount) : undefined;

  return (
    <div className="hc-pagination">
      <div className="hc-pagination__summary">
        {typeof totalCount === "number" && typeof rangeStart === "number" && typeof rangeEnd === "number"
          ? `Showing ${rangeStart}-${rangeEnd} of ${totalCount}`
          : `Page ${currentPage} of ${totalPages}`}
      </div>

      <div className="hc-pagination__controls">
        <Button
          variant="secondary"
          size="sm"
          disabled={currentPage <= 1}
          onClick={() => onPageChange(currentPage - 1)}
        >
          Previous
        </Button>

        <div className="hc-pagination__pages">
          {pages.map((page) => (
            <Button
              key={page}
              variant={page === currentPage ? "primary" : "ghost"}
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
          disabled={currentPage >= totalPages}
          onClick={() => onPageChange(currentPage + 1)}
        >
          Next
        </Button>
      </div>
    </div>
  );
}
