import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it, vi } from "vitest";
import { I18nProvider } from "../../i18n";
import { Pagination } from "./Pagination";

describe("Pagination", () => {
  it("renders the pagination footer for a single page", () => {
    const markup = renderToStaticMarkup(
      <I18nProvider>
        <Pagination
          currentPage={1}
          totalPages={1}
          totalCount={7}
          pageSize={10}
          onPageChange={vi.fn()}
        />
      </I18nProvider>,
    );

    expect(markup).toContain("Showing 1-7 of 7");
    expect(markup).toContain("Previous");
    expect(markup).toContain("Next");
    expect(markup).toContain(">1<");
  });

  it("clamps the visible page summary to the available page range", () => {
    const markup = renderToStaticMarkup(
      <I18nProvider>
        <Pagination
          currentPage={4}
          totalPages={2}
          totalCount={15}
          pageSize={10}
          onPageChange={vi.fn()}
        />
      </I18nProvider>,
    );

    expect(markup).toContain("Showing 11-15 of 15");
    expect(markup).toContain("disabled");
  });
});
